
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Xml.Serialization;
using CR3.CORE;

namespace AFMapper;

/// <summary>
/// Abstract base class of all tables and views in a database.
///
/// Normally this class should not be used in your own code.
/// Instead of this class BaseTableData and BaseViewData should be used.
/// </summary>
public abstract class BaseData : BaseBuffered, IDataObject
{
    private readonly HashSet<string> _lateLoadedFields = new();

    /// <summary>
    /// Primary key of the object.
    /// </summary>
    public virtual Guid PrimaryKey { get; set; } = Guid.Empty;

    /// <summary>
    /// DateTime created
    /// </summary>
    public virtual DateTime CreateDateTime { get; set; } = DateTime.MinValue;

    /// <summary>
    /// DateTime last changed
    /// </summary>
    public virtual DateTime UpdateDateTime { get; set; } = DateTime.MinValue;

    /// <summary>
    /// Marks the object as archived
    /// </summary>
    public virtual bool IsArchived { get; set; } = false;


    /// <summary>
    /// Database from which the object was loaded/in which the object was saved.
    /// </summary>
    [XmlIgnore, Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IDatabase? Database { get; set; }


    /// <summary>
    /// Method that will be raised after a model is loaded from database
    /// </summary>
    public virtual void AfterLoad() { }

    /// <summary>
    /// Method that will be raised before a model will be stored to database
    /// </summary>
    public virtual void BeforeSave() { }

    #region support delayed fields
    /// <summary>
    /// Determines a value provided with the attribute Delayed
    /// </summary>
    /// <typeparam name="T">type of value</typeparam>
    /// <param name="name">name of the property which stores the value</param>
    /// <param name="value">variable which will hold the value</param>
    /// <returns>the read value</returns>
    public T? GetDelayed<T>(ref T? value, [CallerMemberName] string name = "")
    {
        return getDelayed(name, ref value, default);
    }

    /// <summary>
    /// Determines a value provided with the attribute Delayed
    /// </summary>
    /// <typeparam name="T">type of value</typeparam>
    /// <param name="name">name of the property which stores the value</param>
    /// <param name="value">variable which will hold the value</param>
    /// <param name="nullvalue">value that should be used if the read value is null (default value)</param>
    /// <returns>the read value</returns>
    public T GetDelayed<T>(ref T? value, T nullvalue, [CallerMemberName] string name = "")
    {
        T? ret = getDelayed(name, ref value, nullvalue);

        return ret == null ? nullvalue : ret;
    }

    /// <summary>
    /// Checks if the delayed property with the given name is loaded from database
    /// </summary>
    /// <param name="fieldName">name of the property/field</param>
    /// <returns></returns>
    public bool IsDelayedLoaded(string fieldName)
    {
        return _lateLoadedFields.Contains(fieldName);
    }

    /// <summary>
    /// Determines a value provided with the attribute Delayed
    /// </summary>
    /// <typeparam name="T">type of value</typeparam>
    /// <param name="name">name of the property which stores the value</param>
    /// <param name="value">variable which will hold the value</param>
    /// <param name="nullvalue">value that should be used if the read value is null (default value)</param>
    /// <returns>the read value</returns>
    private T? getDelayed<T>(string name, ref T? value, T? nullvalue)
    {
        // prüfen ob der Wert schon geladen wurde...
        if (_lateLoadedFields.Contains(name))
            return value;

        if (Database == null)
            throw new("Missing database. Assign database before use this method.");

        var fld = GetType().GetTypeDescription().Fields.Values.FirstOrDefault(f => f.Name == name);

        if (fld == null || fld.Field == null)
            throw new($"There is no field {name} available.");

        if (fld.Field.Delayed && !PrimaryKey.IsEmpty())
        {
            using var conn = Database.GetConnection();
            value = conn.LoadValue<T>(GetType(), PrimaryKey, name);
        }

        if ((value == null || value is DBNull) && nullvalue != null)
            value = nullvalue;
        
        if (_lateLoadedFields.Contains(name) == false)
            _lateLoadedFields.Add(name);

        return value;
    }
    
    /// <summary>
    /// Set the value of a field that has the Delayed attribute.
    /// </summary>
    /// <typeparam name="T">type of the value</typeparam>
    /// <param name="name">name of the property/field in class</param>
    /// <param name="storein">variable to store the value in</param>
    /// <param name="value">the value to store</param>
    public void SetDelayed<T>(ref T storein, T value, [CallerMemberName] string name = "")
    {
        Set(ref storein, value);

        if (_lateLoadedFields.Contains(name) == false)
            _lateLoadedFields.Add(name);
    }

    /// <summary>
    /// Set the status of the fields marked with the attribute Delayed to NOT LOADED.
    /// </summary>
    public void ResetDelayed()
    {
        _lateLoadedFields.Clear();
    }

    #endregion

    /// <summary>
    /// Returns an object to which a relationship exists by means of the ID
    /// </summary>
    /// <typeparam name="T">Object type</typeparam>
    /// <param name="primaryKey">ID of the object</param>
    /// <returns>the found object or NULL</returns>
    public virtual T? GetRelated<T>(Guid primaryKey) where T : BaseData, new()
    {
        if (Database == null)
        {
            throw new ArgumentException(
                $"Type {typeof(T).FullName} has no Database assigned. Maybe you should overwrite GetDelayed.");
        }

        using var conn = Database.GetConnection();
        return conn.Load<T>(primaryKey);
    }


    /// <summary>
    /// Returns an object to which a relationship exists by means of the ID and
    /// uses a buffer to avoid loading the object again if accessed more then once 
    /// </summary>
    /// <typeparam name="T">Object type</typeparam>
    /// <param name="primaryKey">ID of the object</param>
    /// <param name="buffer">buffer for the loaded object</param>
    /// <returns>the found object or NULL</returns>
    public virtual T? GetRelated<T>(Guid primaryKey, ref T? buffer) where T : BaseData, new()
    {
        if (primaryKey.IsEmpty())
            return null;

        if (buffer != null && buffer.PrimaryKey.Equals(primaryKey))
            return buffer;


        buffer = GetRelated<T>(primaryKey);
        
        return buffer;
    }

    /// <summary>
    /// Copy all Content from a object to the current object
    /// </summary>
    /// <param name="source">source object</param>
    /// <param name="keyFields">copy keyFields (PrimaryKey, CreateAt, LastChanged, Archived) too</param>
    public void CopyFrom(IBindable source, bool keyFields)
    {
        if (source == null)
            throw new("Missing source to copy from.");

        if (source.GetType() != GetType())
            throw new("Type of source is not equals to this type.");

        TypeDescription desc = GetType().GetTypeDescription();

        foreach (PropertyDescription field in desc.Properties.Values)
        {
            if (!((PropertyInfo)field).CanRead || !((PropertyInfo)field).CanWrite) continue;

            if (field.Field == null || field.Field.SystemFieldFlag == eSystemFieldFlag.None || keyFields)
                desc.Accessor[this, ((PropertyInfo)field).Name] = desc.Accessor[source, ((PropertyInfo)field).Name];
        }
    }
}