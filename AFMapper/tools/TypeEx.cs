using System.Reflection;
using System.Runtime.CompilerServices;

namespace AFMapper;

/// <summary>
/// Erweiterungsmethoden für System.Type
/// </summary>
public static class TypeEx
{
    private static readonly Dictionary<Type, TypeDescription> _typedescCache = new();
    private static readonly Dictionary<string, Type> _typeCache = new();

    /// <summary>
    /// Liefert ein Array der TypeDescriptions die eine bestimmte 
    /// Bedingung erfüllen.
    /// 
    /// Die Übergebene Methode bestimmt, die Filterbedingung. 
    /// Liefert die Methode für eine TypeDescription true, ist diese in der Liste enthalten.
    /// </summary>
    /// <param name="predicate">Filterfunktion</param>
    /// <returns></returns>
    public static IEnumerable<TypeDescription> GetTypeDescriptions(Func<TypeDescription, bool> predicate)
    {
        return _typedescCache.Values.Where(predicate);
    }

    /// <summary>
    /// Sucht einen Typen anhand seines Namens in allen geladenen Assemblys
    /// </summary>
    /// <param name="typeName">vollständiger Name des Typs (inkl. NameSpace)</param>
    /// <returns>Type oder NULL wenn nicht gefunden</returns>
    public static Type? FindType(string typeName)
    {
        Type? t = Type.GetType(typeName);

        if (t != null) return t;

        lock (_typeCache)
        {
            if (_typeCache.TryGetValue(typeName, out t)) return t;

            foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
            {
                t = a.GetType(typeName);

                if (t == null) continue;

                _typeCache[typeName] = t;
                break;
            }
        }

        return t;
    }

    /// <summary>
    /// Prüft ob der Typ ein bestimmtes Interface implementiert
    /// </summary>
    /// <param name="type"></param>
    /// <param name="interfaceType"></param>
    /// <returns></returns>
    public static bool HasInterface(this Type type, Type interfaceType)
    {
        return type.GetInterfaces().Contains(interfaceType);
    }

    /// <summary>
    /// Liefert die TypeDescription für einen bestimmten Typen. Das funktioniert nur, 
    /// wenn der Type das Interface IBindable implementiert. Ist das NICHT der Fall, 
    /// wird eine ArgumenException ausgelöst.
    /// </summary>
    /// <param name="type">Type</param>
    /// <returns>TypeDescription für den Typen</returns>
    public static TypeDescription GetTypeDescription(this Type type)
    {
        if (_typedescCache.ContainsKey(type))
            return _typedescCache[type];

        if (!type.HasInterface(typeof(IBindable)))
            throw new ArgumentException($"Type {type.FullName} does not implement CR3.CORE.IBindable.");

        _typedescCache.Add(type, new TypeDescription(type));

        return _typedescCache[type];
    }

    /// <summary>
    /// Alle Typen die vom angegeben Typ erben ermitteln
    /// </summary>
    /// <param name="type">Typ von dem geerbt wird</param>
    /// <returns>Liste der Typen, die vom Typ erben</returns>
    public static IEnumerable<Type> GetChildTypesOf(this Type type)
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .Where(ass => !ass.IsDynamic)
            .SelectMany(t => t.GetTypes())
            .Where(t => t.IsClass &&
                        !t.IsAbstract &&
                        t.IsSubclassOf(type));
    }


    /// <summary>
    /// Liefert alle Erweiterungs-Methoden eines Typs in den aktuell geladenen Assemblies
    /// </summary>
    /// <returns>Rückgabe von MethodInfo[] mit der Erweiterungs-Methode</returns>

    public static IEnumerable<MethodInfo> GetExtensionMethods(this Type t)
    {
        List<Type> types = new();

        foreach (Assembly item in AppDomain.CurrentDomain.GetAssemblies()) types.AddRange(item.GetTypes());

        return from type in types
            where type.IsSealed && !type.IsGenericType && !type.IsNested
            from method in type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            where method.IsDefined(typeof(ExtensionAttribute), false)
            where method.GetParameters()[0].ParameterType == t
            select method;
    }

    /// <summary>
    /// Prüft, ob ein Typ numerisch ist
    /// </summary>
    /// <param name="type">zu prüfender Typ</param>
    /// <returns>true wenn der Typ numerisch ist, sonst false</returns>
    public static bool IsNumericType(this Type type)
    {
        return Type.GetTypeCode(type) switch
        {
            TypeCode.Byte => true,
            TypeCode.SByte => true,
            TypeCode.UInt16 => true,
            TypeCode.UInt32 => true,
            TypeCode.UInt64 => true,
            TypeCode.Int16 => true,
            TypeCode.Int32 => true,
            TypeCode.Int64 => true,
            TypeCode.Decimal => true,
            TypeCode.Double => true,
            TypeCode.Single => true,
            _ => false
        };
    }

    /// <summary>
    /// Sucht nach einer bestimmten Erweiterungs-Methode für den Typ.
    /// </summary>
    /// <param name="methodName">Name der Methode</param>
    /// <param name="t">Type für den die Methode gesucht wird</param>
    /// <param name="seekInParentClasses">Die Methode auch für übergeordnete Type des angegeben Typs suchen</param>
    /// <returns>die gefundene Methode oder null</returns>
    public static MethodInfo? GetExtensionMethod(this Type t, string methodName, bool seekInParentClasses)
    {
        Type seekType = t;

        while (true)
        {
            var mi = seekType.GetExtensionMethods().Where(m => m.Name == methodName).ToArray();

            if (mi.Any()) return mi.First();

            if (!seekInParentClasses)
                break;

            if (seekType.BaseType == null)
                break;

            seekType = seekType.BaseType;
        }

        return null;
    }


    /// <summary>
    /// Prüft ob der Typ dem Wert NULL enstpricht (selbst null oder vom Type Nullable oder vom Typ DBNull
    /// </summary>
    /// <param name="t">zu prüfenden Typ</param>
    /// <returns>true wenn NULL</returns>
    public static bool IsEmpty(this Type t)
    {
        return t == typeof(Nullable) || t == typeof(DBNull);
    }

    /// <summary>
    /// Determine the name of the table of the type if it is provided with the table attribute.
    /// </summary>
    /// <param name="type">type of table</param>
    /// <returns>name or null</returns>
    public static string? GetTableName(this Type type)
    {
        return type.GetTypeDescription().Table?.TableName;
    }

    /// <summary>
    ///  Determine the name of the view of the type if it is provided with the view attribute.
    /// </summary>
    /// <param name="type">type of view</param>
    /// <returns>name or null</returns>
    public static string? GetViewName(this Type type)
    {
        return type.GetTypeDescription().View?.ViewName;
    }

    /// <summary>
    /// Check Name and ID of a Table for uniqueness
    /// </summary>
    /// <param name="type">Type of table</param>
    /// <param name="table">CRTable attribute</param>
    /// <exception cref="Exception">Throws a Exception if TableId or TableName is not unique</exception>
    internal static void checkTable(Type type, AFTable table)
    {
        var found = _typedescCache.Values.FirstOrDefault(t =>
            t.Table != null && t.Table.TableId == table.TableId && t.Type != type);

        if (found != null)
            throw new Exception($"Type {type.FullName} has the same TableId {table.TableId} as {found.Type.FullName}.");

        found = _typedescCache.Values.FirstOrDefault(t =>
            t.Table != null && t.Table.TableName.ToUpper().Trim() == table.TableName.ToUpper().Trim() &&
            t.Type != type);

        if (found != null) 
            throw new Exception($"Type {type.FullName} has the same TableName {table.TableName} as {found.Type.FullName}.");
    }

    /// <summary>
    /// Check Name and ID of a View for uniqueness
    /// </summary>
    /// <param name="type">Type of view</param>
    /// <param name="view">CRView attribute</param>
    /// <exception cref="Exception">Throws a Exception if ViewId or ViewName is not unique</exception>
    internal static void checkView(Type type, AFView view)
    {
        var found = _typedescCache.Values.FirstOrDefault(t =>
            t.View != null && t.View.ViewId == view.ViewId && t.Type != type);

        if (found != null)
            throw new Exception($"Type {type.FullName} has the same ViewId {view.ViewId} as {found.Type.FullName}.");

        found = _typedescCache.Values.FirstOrDefault(t =>
            t.View != null && t.View.ViewName.ToUpper().Trim() == view.ViewName.ToUpper().Trim() &&
            t.Type != type);

        if (found != null) 
            throw new Exception($"Type {type.FullName} has the same ViewName {view.ViewName} as {found.Type.FullName}.");
    }
}

