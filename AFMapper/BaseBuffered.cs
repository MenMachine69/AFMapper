
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Xml.Serialization;

namespace AFMapper;

/// <summary>
/// Base class that captures changes in a buffer to support a RollBack.
/// 
/// All editable models should be derived from this class.
/// </summary>
public abstract class BaseBuffered : Base
{
    private readonly Dictionary<string, object?> _buffer = new();
    private bool _rollBack;


    #region public methods

    /// <summary>
    /// Change a value
    /// </summary>
    /// <typeparam name="T">Type of the value</typeparam>.
    /// <param name="name">Name of the property/field</param>.
    /// <param name="field">Instance variable that holds the value</param>.
    /// <param name="value">new value></param>.
    /// <returns>true if the new value was set, otherwise false</returns>
    public override bool Set<T>(string name, ref T field, T value)
    {
        return Set(name, ref field, value, true);
    }

    /// <summary>
    /// Change a value whose old value should not be buffered.
    /// 
    /// The change cannot be undone with RollBackChanges, HasChanged is NOT set to true by the change.
    /// </summary>
    /// <typeparam name="T">Type of the value</typeparam>.
    /// <param name="name">Name of the property/field</param>.
    /// <param name="field">Instance variable that holds the value</param>.
    /// <param name="value">new value></param>.
    /// <returns>true if the new value was set, otherwise false</returns>
    public bool SetUnbuffered<T>(ref T field, T value, [CallerMemberName] string name = "")
    {
        return Set(name, ref field, value, false);
    }

    /// <summary>
    /// Change a value whose old value should not be buffered.
    /// 
    /// The change cannot be undone with RollBackChanges, HasChanged is NOT set to true by the change.
    /// </summary>
    /// <typeparam name="T">Type of the value</typeparam>.
    /// <param name="name">Name of the property/field</param>.
    /// <param name="field">Instance variable that holds the value</param>.
    /// <param name="value">new value></param>.
    /// <returns>true if the new value was set, otherwise false</returns>
    public bool SetUnbuffered<T>(string name, ref T field, T value)
    {
        return Set(name, ref field, value, false);
    }

    /// <summary>
    /// Change a value
    /// </summary>
    /// <typeparam name="T">Type of the value</typeparam>.
    /// <param name="name">Name of the property/field</param>.
    /// <param name="current">Instance variable that holds the value</param>.
    /// <param name="value">new value></param>.
    /// <param name="buffered">buffer old value</param>.
    /// <returns>true if the new value was set, otherwise false</returns>
    public bool Set<T>(string name, ref T current, T value, bool buffered)
    {
        // das setzen von NULL für einen String verhindern. Stattdessen wird String.Empty verwendet
        if (typeof(T) == typeof(string) && value == null)
            value = (T)(object)string.Empty;

        if (current == null && value == null)
            return false;

        if (current != null && value != null && current.Equals(value))
            return false;

        if (!_rollBack)
        {
            if (buffered)
            {
                if (_buffer.ContainsKey(name) == false)
                    _buffer.Add(name, current);

                HasChanged = true;
            }
        }

        current = value;
        RaisePropertyChangedEvent(name);

        return true;
    }

    /// <summary>
    /// Returns true if a property that supports the PropertyChanged event (set implemented via SetPropertyValue) has been changed.
    /// </summary>
    [XmlIgnore]
    [Browsable(false)]
    public new bool HasChanged { get; private set; }

    /// <summary>
    /// commit all current changes - HasChanged is ALWAYS false after calling this method.
    /// </summary>
    public override void CommitChanges()
    {
        HasChanged = false;
        _buffer.Clear();
    }

    /// <summary>
    /// Revert all changes since the last CommitChanges.
    /// </summary>
    public void RollBackChanges()
    {
        _rollBack = true;
        foreach (var pair in _buffer)
            GetType().GetProperty(pair.Key)?.GetSetMethod()?.Invoke(this, new[] { pair.Value });

        HasChanged = false;
        _buffer.Clear();
        _rollBack = false;
    }

    /// <summary>
    /// List of currently changed properties
    /// </summary>
    public ReadOnlyDictionary<string, object?> ChangedProperties => new(_buffer);

    #endregion
}