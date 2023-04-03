
using System.Collections.ObjectModel;

namespace AFMapper;

/// <summary>
/// Interface for all DataObjects that can be reflected in a database.
///
/// This Interface should not be used directly.
/// For Tables should be used ITable and for Views IView.
/// </summary>
public interface IDataObject
{
    /// <summary>
    /// Primary key of the object.
    /// </summary>
    Guid PrimaryKey { get; set; }

    /// <summary>
    /// DateTime created
    /// </summary>
    DateTime CreateDateTime { get; set; }

    /// <summary>
    /// DateTime last changed
    /// </summary>
    DateTime UpdateDateTime { get; set; }

    /// <summary>
    /// Marks the object as archived
    /// </summary>
    bool IsArchived { get; set; }

    /// <summary>
    /// Database from which the object was loaded/in which the object was saved.
    /// </summary>
    IDatabase? Database { get; set; }

    /// <summary>
    /// Method that will be raised after a model is loaded from database
    /// </summary>
    void AfterLoad() { }

    /// <summary>
    /// Returns true if a property that supports the PropertyChanged event (set implemented via SetPropertyValue) has been changed.
    /// </summary>
    bool HasChanged { get; }

    /// <summary>
    /// commit all current changes - HasChanged is ALWAYS false after calling this method.
    /// </summary>
    void CommitChanges();

    /// <summary>
    /// Revert all changes since the last CommitChanges.
    /// </summary>
    void RollBackChanges();

    /// <summary>
    /// List of currently changed properties
    /// </summary>
    ReadOnlyDictionary<string, object?> ChangedProperties { get; }

    /// <summary>
    /// Checks if the delayed property with the given name is loaded from database
    /// </summary>
    /// <param name="fieldName">name of the property/field</param>
    /// <returns></returns>
    public bool IsDelayedLoaded(string fieldName);
}
