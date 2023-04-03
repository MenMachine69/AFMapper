namespace AFMapper;

/// <summary>
/// EventHub, where events can be subscribed to any object.
/// 
/// The subscriber informs the hub that he is interested in messages (change notifications) for arbitrary objects of a 
/// type (call of subscribe) and receives a token for the subscription from the hub, which he can use to 
/// which he can also use to cancel the subscription (unsubscribe). 
/// 
/// If a change message occurs for an object of the subscribed type (added, changed or deleted), the subscriber's 
/// method of the subscriber is called, which was specified when subscribing. This method is passed the object concerned and 
/// a message indicating what kind of change is involved.
/// 
/// An application can also publish messages itself via the Deliver method.
/// 
/// There should only be one EventHub per application - by default, this is made available via CR.EventHub 
/// without any further action. 
/// </summary>
public sealed class EventHub
{
    private readonly object _locker = new();
    private readonly List<IEventSubscription> _subscriptions = new();

    private class messageSubscription<TObjectType> : IEventSubscription where TObjectType : class
    {
        private readonly Action<TObjectType, eHubEventType, int> _deliverMessage;
        private Func<TObjectType, bool>? _messageFilter;
        private readonly WeakReference _receiver;

        /// <summary>
        /// Initializes a new instance of the <see cref="messageSubscription{TModelType}"/> class.
        /// </summary>
        /// <param name="token">The token that represents the subscription.</param>
        /// <param name="receiver">The object that will receive the messages.</param>
        /// <param name="deliverMessage">A method that will be called to deliver the message to the receiver.</param>
        /// <param name="messageFilter">A method that will be used as a filter for the messages. The messageFilter method will be passed the object before the message is transmitted and must return true if the message should be distributed.</param>
        public messageSubscription(EventToken token, object receiver,
            Action<TObjectType, eHubEventType, int> deliverMessage, Func<TObjectType, bool>? messageFilter)
        {
            Token = token;
            _receiver = new(receiver);
            _deliverMessage = deliverMessage;
            _messageFilter = messageFilter;
        }

        /// <summary>
        /// Gets the subscription's message token.
        /// </summary>
        /// <returns>The subscription's message token.</returns>
        public EventToken Token { get; }

        /// <summary>
        /// Determines whether the subscription can deliver a message to the specified object.
        /// </summary>
        /// <param name="model">The object to check for delivery eligibility.</param>
        /// <returns>True if the subscription can deliver a message to the specified object, false otherwise.</returns>
        public bool CanDeliver(object model)
        {
            // If the receiver is no longer alive, unsubscribe and return false
            if (!_receiver.IsAlive)
            {
                MapperCore.EventHub.Unsubscribe(Token);
                return false;
            }

            // If the model is not the correct type, return false
            if (!typeof(TObjectType).IsAssignableFrom(model.GetType()))
                return false;

            // If the message filter is null or no longer exists, or the model is not the correct type, return true
            if (_messageFilter == null || _messageFilter.Target == null || model is not TObjectType)
                return true;

            // Otherwise, invoke the message filter and return the result
            return ((Func<TObjectType, bool>)_messageFilter.Target).Invoke((TObjectType)model);
        }

        /// <summary>
        /// Delivers a message to the subscribed object.
        /// </summary>
        /// <param name="model">The object to be delivered as a message.</param>
        /// <param name="msgType">The type of message to be delivered.</param>
        public void Deliver(object model, eHubEventType msgType)
        {
            Deliver(model, msgType, 0);
        }

        /// <summary>
        /// Delivers a message to the subscribed object.
        /// </summary>
        /// <param name="model">The object to be delivered as a message.</param>
        /// <param name="msgType">The type of message to be delivered</param>
        /// <param name="messageCode">A code that represents the message being delivered.</param>
        public void Deliver(object model, eHubEventType msgType, int messageCode)
        {
            // If the model is not the correct type, return
            if (!typeof(TObjectType).IsAssignableFrom(model.GetType()))
                return;

            // If the receiver is no longer alive, unsubscribe and return
            if (!_receiver.IsAlive)
            {
                MapperCore.EventHub.Unsubscribe(Token);
                return;
            }

            // If the model is the correct type, invoke the deliver message method
            if (model is TObjectType type)
                _deliverMessage.Invoke(type, msgType, messageCode);
        }

        /// <summary>
        /// Clears the message subscription.
        /// </summary>
        public void Clear()
        {
            // Set the message filter to null
            _messageFilter = null;
        }
    }

    private interface IEventSubscription
    {
        /// <summary>
        /// Gets the subscription's message token.
        /// </summary>
        /// <returns>The subscription's message token.</returns>
        EventToken Token { get; }

        /// <summary>
        /// Determines whether the subscription can deliver a message to the specified object.
        /// </summary>
        /// <param name="model">The object to check for delivery eligibility.</param>
        /// <returns>True if the subscription can deliver a message to the specified object, false otherwise.</returns>
        bool CanDeliver(object model);

        /// <summary>
        /// Delivers a message to the specified object with the specified message type.
        /// </summary>
        /// <param name="model">The object to deliver the message to.</param>
        /// <param name="msgType">The type of message to deliver.</param>
        void Deliver(object model, eHubEventType msgType);

        /// <summary>
        /// Clears the subscription.
        /// </summary>
        void Clear();
    }

    /// <summary>
    /// Subscribe to events
    /// 
    /// A method can be passed here as a filter for the messages. 
    /// </summary>
    /// <typeparam name="TModelType">Type of object</typeparam>
    /// <param name="receiver">The object that will receive the messages.</param>
    /// <param name="deliverMessage">A method that will be called to deliver the message to the receiver.</param>
    /// <param name="messageFilter">A method that will be used as a filter for the messages. 
    /// The messageFilter method will be invoked for the object before the message is transmitted 
    /// and must return true if the message should be distributed.</param>
    /// <returns>A token that describes the subscription.</returns>
    public EventToken Subscribe<TModelType>(object receiver, Action<TModelType, eHubEventType, int> deliverMessage,
        Func<TModelType, bool> messageFilter) where TModelType : class
    {
        EventToken token = new(this, typeof(TModelType));
        
        lock (_locker)
        {
            _subscriptions.Add(new messageSubscription<TModelType>(token, receiver, deliverMessage, messageFilter));
        }
        
        return token;
    }


    /// <summary>
    /// Subscribe to events
    /// 
    /// A method can be passed here that serves as a filter for the messages. This method is passed the object before the 
    /// messages and the method must return true if the message is to be distributed.
    /// </summary>
    /// <typeparam name="TModelType">Type of the object</typeparam>
    /// <param name="deliverMessage">Method to which the event is to be delivered</param>
    /// <param name="receiver">Receiver of the message</param>.
    /// <returns>Token that describes the subscription</returns>
    public EventToken Subscribe<TModelType>(object receiver, Action<TModelType, eHubEventType, int> deliverMessage)
        where TModelType : class
    {
        EventToken token = new(this, typeof(TModelType));
        
        lock (_locker)
        {
            _subscriptions.Add(new messageSubscription<TModelType>(token, receiver, deliverMessage, null));
        }

        return token;
    }

    /// <summary>
    /// Cancel a subscribtione
    /// </summary>
    /// <param name="token">Token of the subscription to be cancelled</param>
    public void Unsubscribe(EventToken token)
    {
        lock (_locker)
        {
            var currentlySubscribed = (from sub in _subscriptions
                where ReferenceEquals(sub.Token, token)
                select sub).ToList();

            currentlySubscribed.ForEach(sub => _subscriptions.Remove(sub));
            currentlySubscribed.ForEach(sub => sub.Clear());
        }
    }

    /// <summary>
    /// Deliver a message to the subscribers
    /// 
    /// Using this method, you can also distribute messages to subscribers yourself.
    /// </summary>
    /// <typeparam name="TModelType">Type of object</typeparam>.
    /// <param name="model">the object that was newly created, modified or deleted</param>.
    /// <param name="msgType">the type of event</param>
    public void Deliver<TModelType>(TModelType model, eHubEventType msgType) where TModelType : class
    {
        List<IEventSubscription> subscriptions;
        lock (_locker)
        {
            subscriptions = (from sub in _subscriptions
                where sub.CanDeliver(model)
                select sub).ToList();
        }

        subscriptions.ForEach(sub => sub.Deliver(model, msgType));

    }
}