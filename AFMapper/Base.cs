using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Xml.Serialization;

namespace AFMapper;

/// <summary>
/// Base class implementing INotifyPropertyChanged.
/// </summary>
[Serializable]
public abstract class Base : INotifyPropertyChanged
{
    [NonSerialized] private bool _changed;

    #region public events

    /// <summary>
    /// Event: Property has been changed
    /// </summary>
    [field: NonSerialized]
    public event PropertyChangedEventHandler? PropertyChanged;

    #endregion

    #region public methods

    /// <summary>
    /// Triggers the PropertyChanged event
    /// </summary>
    /// <param name="property">Name of the property whose value has changed.</param>
    public void RaisePropertyChangedEvent(string property)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
    }


    /// <summary>
    /// Change a value
    /// 
    /// This method does NOT allow null values (zero or DBNull).
    /// 
    /// If null or DBNull is passed as a value, an ArgumentNullException is thrown.
    /// </summary>
    /// <typeparam name="T">Type of the value</typeparam>.
    /// <param name="name">Name of the property/field</param>.
    /// <param name="field">Instance variable that holds the value</param>.
    /// <param name="value">new value></param>.
    /// <returns>true if the new value was set, otherwise false (= new value corresponds to the already existing one)</returns>
    public virtual bool SetNotNullable<T>(string name, ref T field, T value)
    {
        if (value == null || value is DBNull) throw new ArgumentNullException(name);

        return Set(name, ref field, value);
    }

    /// <summary>
    /// Change a value
    /// </summary>
    /// <typeparam name="T">Type of the value</typeparam>.
    /// <param name="name">Name of the property/field</param>.
    /// <param name="field">Instance variable that holds the value</param>.
    /// <param name="value">new value></param>.
    /// <returns>true if the new value was set, otherwise false (= new value corresponds to the already existing one)</returns>
    public virtual bool SetNotNullable<T>(ref T field, T value, [CallerMemberName] string name = "")
    {
        if (string.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));

        return SetNotNullable(name, ref field, value);
    }

    /// <summary>
    /// Ändern eines Wertes
    /// </summary>
    /// <typeparam name="T">Typ des Wertes</typeparam>
    /// <param name="name">Name der Eigenschaft/des Feldes</param>
    /// <param name="field">Instanzvariable, die den Wert aufnimmt</param>
    /// <param name="value">neuer Wert></param>
    /// <returns>true, wenn der neue Wert gesetzt wurde, sonst false</returns>
    public virtual bool Set<T>(ref T field, T value, [CallerMemberName] string name = "")
    {
        if (string.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));

        return Set(name, ref field, value);
    }

    /// <summary>
    /// Change a value
    /// </summary>
    /// <typeparam name="T">Type of the value</typeparam>.
    /// <param name="name">Name of the property/field</param>.
    /// <param name="current">Instance variable that holds the value</param>.
    /// <param name="value">new value></param>.
    /// <returns>true if the new value was set, otherwise false</returns>
    public virtual bool Set<T>(string name, ref T current, T value)
    {
        // das setzen von NULL für einen String verhindern. Stattdessen wird String.Empty verwendet
        if (typeof(T) == typeof(string) && value == null)
            value = (T)(object)string.Empty;

        if (current == null && value == null)
            return false;

        if (current != null && value != null && current.Equals(value))
            return false;

        current = value;
        _changed = true;
        RaisePropertyChangedEvent(name);

        return true;
    }

    /// <summary>
    /// Returns true if a property that supports the PropertyChanged event (set implemented via SetPropertyValue) has been changed.
    /// </summary>
    [XmlIgnore]
    [Browsable(false)]
    public bool HasChanged => _changed;

    /// <summary>
    /// commit all current changes - HasChanged is ALWAYS false after calling this method.
    /// </summary>
    public virtual void CommitChanges()
    {
        _changed = false;
    }
    #endregion
}