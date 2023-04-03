
namespace AFMapper;

/// <summary>
/// Constants for database specific SQL statements
/// </summary>
public enum eDatabaseConstant
{
    /// <summary>
    /// ASC/ASCENDING
    /// </summary>
    asc,
    /// <summary>
    /// DESC/DESCENDING
    /// </summary>
    desc,
    /// <summary>
    /// UNIQUE
    /// </summary>
    unique,
    /// <summary>
    /// NOT UNIQUE
    /// </summary>
    notunique
}

/// <summary>
/// interface for a database that can contain tables and views
/// </summary>
public interface IDatabase
{
    /// <summary>
    /// Configuration of the database
    /// </summary>
    IConfiguration Configuration { get; set; }

    /// <summary>
    /// Open a connection to this database.
    /// </summary>
    /// <returns></returns>
    IConnection GetConnection();

    /// <summary>
    /// Database credentials for normal connections
    /// </summary>
    void SetCredentials(Tuple<string, string> credentials);

    /// <summary>
    /// Database credentials for administrative connections
    /// </summary>
    void SetAdminCredentials(Tuple<string, string> credentials);
    

    /// <summary>
    /// Translate a field, table or view name to the 
    /// propper format for this database using database naming conventions
    /// </summary>
    /// <param name="original"></param>
    /// <returns>translated name</returns>
    string GetName(string original);

    /// <summary>
    /// Translate a constant into a database specific string
    /// </summary>
    /// <param name="constant">constant</param>
    /// <returns>the string</returns>
    string GetConstant(eDatabaseConstant constant);

    /// <summary>
    /// Naming conventions for tables and fields
    /// 
    /// default is eDatabaseNamingScheme.original
    /// </summary>
    eDatabaseNamingScheme NamingConventions { get; set; }
    
    /// <summary>
    /// Translator fpr this database.
    /// </summary>
    ITranslator Translator { get; }
    
    /// <summary>
    /// Create database if not exist.
    /// </summary>
    void Create();


    /// <summary>
    /// Check database types (tables and views)
    /// </summary>
    /// <param name="tableTypes"></param>
    /// <param name="viewTypes"></param>
    /// <param name="feedback"></param>
    /// <param name="force">force/full check database</param>
    /// <returns>true if ok, otherwise false</returns>
    bool Check(List<Type> tableTypes, List<Type> viewTypes, Action<string> feedback, bool force);

    /// <summary>
    /// Check if database exist.
    /// </summary>
    bool Exist { get; }
    

    /// <summary>
    /// Action that is executed - when tracing is on - before an SQL statement is executed.
    /// </summary>
    Action<TraceInfo>? TraceBeforeExecute { get; set; }

    /// <summary>
    /// Action that is executed - when tracing is on - after an SQL statement is executed.
    /// </summary>
    Action<TraceInfo>? TraceAfterExecute { get; set; }

    /// <summary>
    /// Action to be executed after a data object has been changed/saved (if tracing is on).
    /// </summary>
    Action<IDataObject>? AfterSave { get; set; }

    /// <summary>
    /// Action to be executed after a data object has been deleted (if tracing is on).
    /// </summary>
    Action<IDataObject>? AfterDelete { get; set; }

    /// <summary>
    /// a factory that can create ILogger-Objects
    /// </summary>
    ILoggerFactory? LoggerFactory { get; set; }
}