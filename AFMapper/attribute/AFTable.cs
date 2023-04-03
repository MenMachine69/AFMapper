
namespace AFMapper;

/// <summary>
/// Attribute that describes a table in the database
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class AFTable : Attribute
{
    /// <summary>
    /// Name of the table in the database.
    /// If no name is given, the name of the table corresponds to the name of the type preceded by TBL_
    /// 
    /// Sample: Type: Firma
    ///         Table name: TBL_FIRMA (Notation depending on database configuration - Large, Small or Mixed)
    /// 
    /// </summary>
    public string TableName { get; set; } = "";

    /// <summary>
    /// Database version with which the table was/is to be adjusted the last time.
    /// 
    /// If the version is increased, the structure of the table is checked when the database is checked.
    /// </summary>
    public int Version { get; set; }

    /// <summary>
    /// Internal ID of the table
    /// 
    /// IDs from 0 - 100 are reserved for CR3 internal things. For own tables use only IDs &gt; 100!
    /// </summary>
    public int TableId { get; set; }

    /// <summary>
    /// Use cache for the data (data is cached if possible and read from the cache instead of the database).
    /// </summary>
    public bool UseCache { get; set; }

    /// <summary>
    /// Log changes. Logging must be switched on for each individual CRField and a handler must be assigned to the 
    /// database must be assigned a handler for logging.
    /// </summary>
    public bool LogChanges { get; set; }
}