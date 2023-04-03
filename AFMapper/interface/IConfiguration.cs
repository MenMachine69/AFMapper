
namespace AFMapper;

/// <summary>
/// Interface for implementation by classes representing database configurations.
/// </summary>
public interface IConfiguration
{
    /// <summary>
    /// Type/base class of the table classes of this database
    /// 
    /// Check will use this type to detect all types which has to be checked.
    /// </summary>
    List<Type> BaseTableTypes { get; set; }

    /// <summary>
    /// Type/base class of the view classes of this database
    /// 
    /// Check will use this type to detect all types which has to be checked.
    /// </summary>
    List<Type> BaseViewTypes { get; set; }

    /// <summary>
    /// Connecstring for the database
    /// </summary>
    /// <returns>valid connectsring for accessing the database based on the settings</returns>
    string ConnectionString { get; }

    /// <summary>
    /// Name of the database
    /// </summary>
    string DatabaseName { get; }

    /// <summary>
    /// Conflict handling for the database
    /// </summary>
    eConflictMode ConflictMode { get; }

    /// <summary>
    /// database type
    /// </summary>
    eDatabaseType DatabaseType { get; }

    /// <summary>
    /// Indicates whether columns/fields of the tables can be deleted during the update, 
    /// if there is no more field for this column in the class of the model.
    /// </summary>
    bool AllowDropColumns { get; set; }

    /// <summary>
    /// Create a database object based on the configuration
    /// </summary>
    /// <returns></returns>
    public IDatabase Create();
}