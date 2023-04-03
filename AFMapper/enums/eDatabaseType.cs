
namespace AFMapper;

/// <summary>
/// Type of database
/// </summary>
public enum eDatabaseType
{
    /// <summary>
    /// not defined database type
    /// </summary>
    Unknown = 0,
    /// <summary>
    /// Microsoft SQL database
    /// </summary>
    MsSql = 1,
    /// <summary>
    /// Microsoft Azure SQL database
    /// </summary>
    AzureSql = 2,
    /// <summary>
    /// PostgreSQL database
    /// </summary>
    PostgreSql = 3,
    /// <summary>
    /// Firebird SQL database (server)
    /// </summary>
    FirebirdSql = 4,
    /// <summary>
    /// Firebird SQL database (embedded)
    /// </summary>
    FirebirdEmbeddedSql = 5,
}