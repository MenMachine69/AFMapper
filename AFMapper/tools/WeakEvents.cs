
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace CR3.CORE
{
    /// <summary>
    /// Class for mapping so-called WeakEvents
    /// </summary>
    /// <typeparam name="T">The delegate type of the event handlers.</typeparam>
    public sealed class WeakEvent<T> where T : class
    {
        static WeakEvent()
        {
            if (!typeof(T).IsSubclassOf(typeof(Delegate)))
                throw new ArgumentException("T must be a delegate type");
            MethodInfo? invoke = typeof(T).GetMethod("Invoke");
            if (invoke == null)
                throw new ArgumentException("T must be a delegate type");
            if (invoke.ReturnType != typeof(void))
                throw new ArgumentException("The delegate return type must be void.");
            foreach (var p in invoke.GetParameters())
            {
                if (p.IsOut && !p.IsIn)
                    throw new ArgumentException("The delegate type must not have out-parameters");
            }
        }

        private class HandlerEntry
        {
            public readonly WeakEvent<T> ParentEventSource;
            private readonly WeakReference weakReference;
            public readonly MethodInfo TargetMethod;
            public Delegate? WrappingDelegate;
            public HandlerEntry(WeakEvent<T> parentEventSource, object targetInstance, MethodInfo targetMethod)
            {
                ParentEventSource = parentEventSource;
                weakReference = new WeakReference(targetInstance);
                TargetMethod = targetMethod;
            }
            // This property is accessed by the generated IL method
            public object? TargetInstance => weakReference.Target;

            // This method is called by the generated IL method
            public void CalledWhenDead()
            {
                if (WrappingDelegate != null)
                    ParentEventSource.RemoveFromRaiseDelegate(WrappingDelegate);
            }
            /*
            A wrapper method like this is generated using IL.Emit and attached to this object.
            The signature of the method depends on the delegate type T.
            this.WrappingDelegate = delegate(object sender, EventArgs e)
            {
            object target = this.TargetInstance;
            if (target == null)
            this.CalledWhenDead();
            else
            ((TargetType)target).TargetMethod(sender, e);
            }
            */
        }

        private volatile Delegate? _raiseDelegate;

        private Delegate? GetRaiseDelegateInternal()
        {
            return _raiseDelegate;
        }

#pragma warning disable 420 // CS0420 - a reference to a volatile field will not be treated as volatile
        // can be ignored because CompareExchange() treats the reference as volatile
        private void AddToRaiseDelegate(Delegate d)
        {
            Delegate? oldDelegate, newDelegate;
            do
            {
                oldDelegate = _raiseDelegate;
                newDelegate = Delegate.Combine(oldDelegate, d);
            } while (Interlocked.CompareExchange(ref _raiseDelegate, newDelegate, oldDelegate) != oldDelegate);
        }

        private void RemoveFromRaiseDelegate(Delegate d)
        {
            Delegate? oldDelegate, newDelegate;
            do
            {
                oldDelegate = _raiseDelegate;
                newDelegate = Delegate.Remove(oldDelegate, d);
            } while (Interlocked.CompareExchange(ref _raiseDelegate, newDelegate, oldDelegate) != oldDelegate);
        }
#pragma warning restore 420
        /// <summary>
        /// Add subscriber
        /// </summary>
        /// <param name="eh">Subscriber</param>
        public void Add(T eh)
        {
            Delegate d = (Delegate)(object)eh;
            RemoveDeadEntries();
            object? targetInstance = d.Target;
            if (targetInstance != null)
            {
                MethodInfo targetMethod = d.Method;
                var wd = new HandlerEntry(this, targetInstance, targetMethod);
                var dynamicMethod = GetInvoker(targetMethod);
                
                if (dynamicMethod == null) return;

                wd.WrappingDelegate = dynamicMethod.CreateDelegate(typeof(T), wd);
                AddToRaiseDelegate(wd.WrappingDelegate);
            }
            else
            {
                // delegate to static method: use directly without wrapping delegate
                AddToRaiseDelegate(d);
            }
        }

        /// <summary>
        /// Removes dead entries from the handler list.
        /// You normally do not need to invoke this method manually, as dead entry removal runs automatically as part of the normal operation of the WeakEvent.
        /// </summary>
        public void RemoveDeadEntries()
        {
            Delegate? raiseDelegate = GetRaiseDelegateInternal();
            if (raiseDelegate == null)
                return;
            foreach (var d in raiseDelegate.GetInvocationList())
            {
                if (d.Target is HandlerEntry wd && wd.TargetInstance == null)
                    RemoveFromRaiseDelegate(d);
            }
        }

        /// <summary>
        /// Remove subscriber
        /// </summary>
        /// <param name="eh">Subscriber</param>
        public void Remove(T eh)
        {
            Delegate d = (Delegate)(object)eh;
            object? targetInstance = d.Target;
            if (targetInstance == null)
            {
                // delegate to static method: use directly without wrapping delegate
                RemoveFromRaiseDelegate(d);
                return;
            }
            MethodInfo targetMethod = d.Method;
            // Find+Remove the last copy of a delegate pointing to targetInstance/targetMethod
            Delegate? raiseDelegate = GetRaiseDelegateInternal();
            if (raiseDelegate == null)
                return;
            Delegate[] invocationList = raiseDelegate.GetInvocationList();
            for (int i = invocationList.Length - 1; i >= 0; i--)
            {
                var wrappingDelegate = invocationList[i];
                var weakDelegate = wrappingDelegate.Target as HandlerEntry;
                if (weakDelegate == null)
                    continue;
                object? target = weakDelegate.TargetInstance;
                if (target == null)
                    RemoveFromRaiseDelegate(wrappingDelegate);
                else if (target == targetInstance && weakDelegate.TargetMethod == targetMethod)
                {
                    RemoveFromRaiseDelegate(wrappingDelegate);
                    break;
                }
            }
        }
        /// <summary>
        /// Retrieves a delegate that can be used to raise the event.
        /// The delegate will contain a copy of the current invocation list. If handlers are added/removed from the event, GetRaiseDelegate() must be called
        /// again to retrieve a delegate that invokes the up-to-date invocation list.
        ///
        /// If the invocation list is empty, this method will return null.
        /// </summary>
        public T? GetRaiseDelegate()
        {
            var obj = GetRaiseDelegateInternal();

            if (obj != null && obj is T)
                return (T)(object)obj;

            return null;
        }
        /// <summary>
        /// Gets whether the event has listeners that were not cleaned up yet.
        /// </summary>
        public bool HasListeners => GetRaiseDelegateInternal() != null;

        #region Code Generation

        private static readonly MethodInfo? getTargetMethod = typeof(HandlerEntry).GetMethod("get_TargetInstance");
        private static readonly MethodInfo? calledWhileDeadMethod = typeof(HandlerEntry).GetMethod("CalledWhenDead");
        // ReSharper disable once StaticMemberInGenericType
        private static readonly Dictionary<MethodInfo, DynamicMethod> invokerMethods = new();

        private static DynamicMethod? GetInvoker(MethodInfo method)
        {
            DynamicMethod? dynamicMethod;
            lock (invokerMethods)
            {
                if (invokerMethods.TryGetValue(method, out dynamicMethod))
                    return dynamicMethod;
            }

            if (method.DeclaringType?.GetCustomAttributes(typeof(CompilerGeneratedAttribute), false).Length != 0)
                throw new ArgumentException("Cannot create weak event to anonymous method with closure.");
            var parameters = method.GetParameters();
            var dynamicMethodParameterTypes = new Type[parameters.Length + 1];
            dynamicMethodParameterTypes[0] = typeof(HandlerEntry);
            for (int i = 0; i < parameters.Length; i++) dynamicMethodParameterTypes[i + 1] = parameters[i].ParameterType;

            if (getTargetMethod == null || calledWhileDeadMethod == null) return dynamicMethod;

            dynamicMethod = new DynamicMethod("WeakEvent", typeof(void), dynamicMethodParameterTypes,
                typeof(HandlerEntry), true);
            ILGenerator il = dynamicMethod.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.EmitCall(OpCodes.Call, getTargetMethod, null);
            il.Emit(OpCodes.Dup);
            var label = il.DefineLabel();
            // Exit if target is null (was garbage-collected)
            il.Emit(OpCodes.Brtrue, label);
            il.Emit(OpCodes.Pop); // pop the duplicate null target
            il.Emit(OpCodes.Ldarg_0);
            il.EmitCall(OpCodes.Call, calledWhileDeadMethod, null);
            il.Emit(OpCodes.Ret);
            il.MarkLabel(label);
            il.Emit(OpCodes.Castclass, method.DeclaringType);
            for (int i = 0; i < parameters.Length; i++) il.Emit(OpCodes.Ldarg, i + 1);

            il.EmitCall(OpCodes.Call, method, null);
            il.Emit(OpCodes.Ret);
            lock (invokerMethods)
            {
                invokerMethods[method] = dynamicMethod;
            }


            return dynamicMethod;
        }

        #endregion
    }

    /// <summary>
    /// Strongly-typed raise methods for WeakEvent
    /// </summary>
    public static class WeakEventRaiseExtensions
    {
        /// <summary>
        /// Triggers the events
        /// </summary>
        /// <param name="ev">Event handler</param>
        /// <param name="sender">Sender</param>
        /// <param name="e">Parameter</param>
        public static void Raise(this WeakEvent<EventHandler> ev, object sender, EventArgs e)
        {
            var d = ev.GetRaiseDelegate();
            d?.Invoke(sender, e);
        }

        /// <summary>
        /// Triggers the events (strong typed)
        /// </summary>
        /// <typeparam name="T">Type of Argumente</typeparam>
        /// <param name="ev">Event handler</param>
        /// <param name="sender">Sender</param>
        /// <param name="e">Parameter</param>
        public static void Raise<T>(this WeakEvent<EventHandler<T>> ev, object sender, T e) where T : EventArgs
        {
            var d = ev.GetRaiseDelegate();
            d?.Invoke(sender, e);
        }
    }
}
