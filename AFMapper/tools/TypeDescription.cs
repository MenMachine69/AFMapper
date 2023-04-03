
using System.Data.SqlTypes;
using System.Reflection;
using FastMember;

namespace AFMapper;

/// <summary>
/// Description of a type/class with simplified access to the properties. 
/// and additional information about them (attributes of the type and properties)
/// 
/// This description can be accessed via typeof(Example).GetTypeDescription().
/// 
/// Requirement: the type must implement the interface IBindable!
/// 
/// If the type contains a static method AfterRegisterTypeDescription, it is automatically called and the 
/// and the TypeDescription is passed to this method. This way the TypeDescription can be 
/// e.g. further information can be added to the TypeDescription.
/// </summary>
public sealed class TypeDescription
{
    private readonly Dictionary<string, object> _extensions = new();
    private Dictionary<string, PropertyDescription>? _fields;


    /// <summary>
    /// Constructor
    /// 
    /// This constructor is only internal. The TypeDescription will bee created by typeof(Model).GetTypeDescription()
    /// </summary>
    /// <param name="type">described type</param>
    internal TypeDescription(Type type)
    {
        Type = type;

        Table = type.GetCustomAttribute<AFTable>();
        View = type.GetCustomAttribute<AFView>();

        Accessor = TypeAccessor.Create(type);

        foreach (PropertyInfo property in type.GetProperties(BindingFlags.FlattenHierarchy | BindingFlags.GetProperty |
                                                             BindingFlags.SetProperty | BindingFlags.Public |
                                                             BindingFlags.Instance))
            Properties.Add(property.Name, PropertyDescription.Create(property));

        // Überprüfung der Attribute
        if (Table != null && View != null)
            throw new ArgumentException($"Type {type.FullName} can not be Table and View at the same time.");

        if (Table != null)
        {
            if (Table.TableId <= 0)
                throw new ArgumentException($"Type {type.FullName} is a table but has no TableId > 0.");

            if (Table.Version <= 0)
                throw new ArgumentException($"Type {type.FullName} is a table but Version is not > 0. The version must be always > 0.");

            if (Table.TableId < 100 && !type.FullName!.ToUpper().StartsWith("CR3.DATA"))
                throw new ArgumentException($"Type {type.FullName} is a table but has a TableId < 100. TableIds < 100 are reserved for CR3 internal tables.");

            TypeEx.checkTable(type, Table);
        }

        if (View != null)
        {
            if (View.ViewId <= 0)
                throw new ArgumentException($"Type {type.FullName} is a view but has no ViewId > 0.");

            if (View.Version <= 0)
                throw new ArgumentException($"Type {type.FullName} is a view but Version is not > 0. The version must be always > 0.");

            if (View.ViewId < 100 && type.FullName!.ToUpper().StartsWith("CR3.DATA"))
                throw new ArgumentException($"Type {type.FullName} is a view but has a ViewId < 100. ViewIds < 100 are reserved for CR3 internal views.");

            TypeEx.checkView(type, View);
        }

        // AfterRegisterTypeDescription aufrufen, wenn verfügbar...
        MethodInfo? ifo = type.GetMethod("AfterRegisterTypeDescription",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.DeclaredOnly | BindingFlags.IgnoreCase);

        ifo?.Invoke(null, null);
    }

    /// <summary>
    /// Erweiterungsobjekt zurückgeben
    /// </summary>
    /// <typeparam name="T">Typ des Objekts</typeparam>
    /// <param name="name">Name des Objekts</param>
    /// <returns>Objekt vom Typ T oder null, wenn nicht vorhanden</returns>
    public T? GetExtension<T>(string name) where T : INullable
    {
        T? ret = default;

        if (_extensions.ContainsKey(name))
            return (T)_extensions[name];

        return ret;
    }

    /// <summary>
    /// Erweiterungsobjekt hinzufügen
    /// </summary>
    /// <param name="name">Name</param>
    /// <param name="value">Wert/Objekt</param>
    public void SetExtension(string name, object value)
    {
        _extensions.Add(name, value);
    }

    /// <summary>
    /// operator which allows to use TypeDescription objects directly as Type
    /// </summary>
    /// <param name="typeDescription"></param>
    public static explicit operator Type(TypeDescription typeDescription)
    {
        return typeDescription.Type;
    }

    /// <summary>
    /// Type which is described by this TypeDescription
    /// </summary>
    public Type Type { get; private set; }

    /// <summary>
    /// Table attribute of the Type if Type represents a Table
    /// </summary>
    public AFTable? Table { get; private set; }

    /// <summary>
    /// Table attribute of the Type if Type represents a Table
    /// </summary>
    public AFView? View { get; private set; }

    /// <summary>
    /// True if type is a table
    /// </summary>
    public bool IsTable => Table != null;

    /// <summary>
    /// True if type is a view
    /// </summary>
    public bool IsView => View != null;


    /// <summary>
    /// a list of all fields in this type
    /// </summary>
    public Dictionary<string, PropertyDescription> Properties { get; } = new();

    /// <summary>
    /// Access to the TypeAccessor of the type (see FastMember)
    /// </summary>
    public TypeAccessor Accessor { get; }

    /// <summary>
    /// eine Liste aller Felder (Eigenschaften mit dem Attribut CRField)
    /// </summary>
    /// <returns></returns>
    public Dictionary<string, PropertyDescription> Fields =>
        _fields ??= Properties.Where(p => p.Value.Field != null).ToDictionary(p => p.Key, p => p.Value);

    /// <summary>
    /// Eigenschaft, die als PrimaryKey.
    /// </summary>
    public PropertyDescription? FieldKey =>
        Properties.Values.FirstOrDefault(p => p.Field?.SystemFieldFlag == eSystemFieldFlag.PrimaryKey);

    /// <summary>
    /// Eigenschaft, die als TimeStamp für den Zeitpunkt der ersten Speicherung verwendet wird.
    /// </summary>
    public PropertyDescription? FieldCreated =>
        Properties.Values.FirstOrDefault(p => p.Field?.SystemFieldFlag == eSystemFieldFlag.TimestampCreated);

    /// <summary>
    /// Eigenschaft, die als TimeStamp für den Zeitpunkt der letzten Speicherung verwendet wird.
    /// </summary>
    public PropertyDescription? FieldChanged =>
        Properties.Values.FirstOrDefault(p => p.Field?.SystemFieldFlag == eSystemFieldFlag.TimestampChanged);

    /// <summary>
    /// Eigenschaft, die als Flag für die Archivierung verwendet wird
    /// </summary>
    public PropertyDescription? FieldArchived =>
        Properties.Values.FirstOrDefault(p => p.Field?.SystemFieldFlag == eSystemFieldFlag.ArchiveFlag);
}




/// <summary>
/// Description of a property in a class.
/// 
/// Can be casted to PropertyInfo.
/// </summary>
public class PropertyDescription
{
    /// <summary>
    /// the decorated PropertyInfo
    /// </summary>
    private PropertyInfo _propertyInfo;

    /// <summary>
    /// hide the parameterless constructor
    /// </summary>
    private PropertyDescription(PropertyInfo propertyInfo)
    {
        _propertyInfo = propertyInfo;
    }

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="propertyInfo">the PropertyInfo which is represented by this object</param>
    public static PropertyDescription Create(PropertyInfo propertyInfo)
    {
        PropertyDescription ret = new(propertyInfo)
        {
            Field = propertyInfo.GetCustomAttribute<AFField>(true),
        };

        return ret;
    }

    /// <summary>
    /// Name des Propertys
    /// </summary>
    public string Name => _propertyInfo.Name;

    /// <summary>
    /// operator which allows to use PropertyDescription objects directly as PropertyInfo
    /// </summary>
    /// <param name="propertyDescription"></param>
    public static explicit operator PropertyInfo(PropertyDescription propertyDescription)
    {
        return propertyDescription._propertyInfo;
    }

    /// <summary>
    /// Rules for this property
    /// </summary>
    public AFField? Field { get; init; }
}

