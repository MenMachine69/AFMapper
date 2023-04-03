
namespace AFMapper;

/// <summary>
/// abstract base class for all databases
/// </summary>
public abstract class BaseDatabase : IDatabase
{
    private Action<TraceInfo>? _traceStart;
    private Action<TraceInfo>? _traceEnd;
    private Action<TraceInfo>? _beforeExecute;
    private Action<TraceInfo>? _afterExecute;
    private Action<IDataObject>? _afterSave;
    private Action<IDataObject>? _afterDelete;

    /// <summary>
    /// Create a database
    /// </summary>
    /// <param name="config">database configuration</param>
    protected BaseDatabase(IConfiguration config)
    {
        Configuration = config;
    }

    /// <summary>
    /// Translate a field, table or view name to the 
    /// propper format for this database using database nameing conventions
    /// </summary>
    /// <param name="original"></param>
    /// <returns>translated name</returns>
    public string GetName(string original)
    {
        switch (NamingConventions)
        {
            case eDatabaseNamingScheme.original:
                return original;
            case eDatabaseNamingScheme.lowercase:
                return original.ToLower();
            case eDatabaseNamingScheme.uppercase:
                return original.ToUpper();
            default:
                return original;
        }
    }

    /// <summary>
    /// Translate a constant into a database specific string
    /// </summary>
    /// <param name="constant">constant</param>
    /// <returns>the string</returns>
    public virtual string GetConstant(eDatabaseConstant constant)
    {
        if (eDatabaseConstant.asc == constant)
            return "ASC";
        if (eDatabaseConstant.desc == constant)
            return "DESC";
        if (eDatabaseConstant.unique == constant)
            return "UNIQUE";
        if (eDatabaseConstant.notunique == constant)
            return "";

        return "";
    }

    /// <summary>
    /// Naming conventions for tables and fields
    /// 
    /// default is eDatabaseNamingScheme.original
    /// </summary>
    public eDatabaseNamingScheme NamingConventions { get; set; } = eDatabaseNamingScheme.original;

    /// <summary>
    /// Configuration of this database
    /// </summary>
    public IConfiguration Configuration { get; set; }

    /// <summary>
    /// If this property is true, all events (TraceStart, TraceEnd, AfterSave and AfterDelete) will be suppressed
    /// </summary>
    public bool Silent { get; set; }

    /// <summary>
    /// event action for TraceStart
    /// 
    /// This action delegate will be executed in case of an TraceStart event inside the database.
    /// </summary>
    public Action<TraceInfo>? TraceStart { get => Silent ? null : _traceStart; set => _traceStart = value; }

    /// <summary>
    /// event action for TraceEnd
    /// 
    /// This action delegate will be executed in case of an TraceEnd event inside the database.
    /// </summary>
    public Action<TraceInfo>? TraceEnd { get => Silent ? null : _traceEnd; set => _traceEnd = value; }

    /// <summary>
    /// event action for AfterSave
    /// 
    /// This action delegate will be executed in case of an AfterSave event inside the database.
    /// </summary>
    public Action<IDataObject>? AfterSave { get => Silent ? null : _afterSave; set => _afterSave = value; }

    /// <summary>
    /// event action for AfterDelete
    /// 
    /// This action delegate will be executed in case of an AfterSave event inside the database.
    /// </summary>
    public Action<IDataObject>? AfterDelete { get => Silent ? null : _afterDelete; set => _afterDelete = value; }

    /// <summary>
    /// Action to log changes. 
    /// 
    /// This Action will be called from Save an a array of 
    /// tuples with changed values will be submitted. Each tuple contains ID of entry, old value, new value.
    /// </summary>
    public ILoggerFactory? LoggerFactory { get; set; }

    /// <summary>
    /// Translator for the database.
    /// 
    /// This method must be overwritten in the concrete database class.
    /// </summary>
    /// <exception cref="NotImplementedException"></exception>
    public virtual ITranslator Translator => throw new NotImplementedException();

    /// <summary>
    /// Check database types (tables and views)
    /// </summary>
    /// <param name="tableTypes">List of base table types of the database</param>
    /// <param name="viewTypes">List of base view types of the database</param>
    /// <param name="feedback">action for feedback about the status of checking</param>
    public void Check(List<Type> tableTypes, List<Type> viewTypes, Action<string> feedback)
    {
        if (tableTypes.Count > 0)
        {
            // Tabellen prüfen...
            foreach (var type in tableTypes.Select(tableType => 
                         tableType.GetChildTypesOf()).SelectMany(tblTypes => 
                         tblTypes.Where(t => t.HasInterface(typeof(ITable)))))
            {
                feedback?.Invoke($"Check table {type.Name}...");

                using var conn = GetConnection();
                conn.Check(type, false);
            }
        }

        if (viewTypes.Count <= 0) return;

        foreach (var type in viewTypes.SelectMany(viewType =>
                     viewType.GetChildTypesOf().Where(t => t.HasInterface(typeof(IView)))))
        {
            feedback?.Invoke($"Check view {type.Name}...");

            using var conn = GetConnection();
            conn.Check(type, false);
        }
    }

    /// <summary>
    /// Type/base class of the table classes of this database
    /// 
    /// Check will use this type to detect all types which has to be checked.
    /// </summary>
    public List<Type> BaseTableTypes { get; set; } = new();

    /// <summary>
    /// Type/base class of the view classes of this database
    /// 
    /// Check will use this type to detect all types which has to be checked.
    /// </summary>
    public List<Type> BaseViewTypes { get; set; } = new();

    /// <summary>
    /// Check database structure
    /// </summary>
    /// <param name="tableTypes">table class types</param>
    /// <param name="viewTypes">view class types</param>
    /// <param name="feedback">feedback while check</param>
    /// <param name="force">force complete structure check</param>
    /// <returns>true if structure checked without errors</returns>
    public bool Check(List<Type> tableTypes, List<Type> viewTypes, Action<string> feedback, bool force)
    {

        // TODO: implementieren!
        return true;
    }

    /// <summary>
    /// Check if database exist.
    /// 
    /// This method must be overwritten in the concrete database class.
    /// </summary>
    /// <exception cref="NotImplementedException"></exception>
    public virtual bool Exist => throw new NotImplementedException();

    /// <summary>
    /// event action for AfterDelete
    /// 
    /// This action delegate will be executed in case of an TraceBeforeExecute event inside the database.
    /// </summary>
    public Action<TraceInfo>? TraceBeforeExecute { get => Silent ? null : _beforeExecute; set => _beforeExecute = value; }

    /// <summary>
    /// event action for AfterDelete
    /// 
    /// This action delegate will be executed in case of an TraceAfterExecute event inside the database.
    /// </summary>
    public Action<TraceInfo>? TraceAfterExecute { get => Silent ? null : _afterExecute; set => _afterExecute = value; }

    /// <summary>
    /// Create a database
    /// 
    /// This method must be overwritten in the concrete database class.
    /// </summary>
    /// <exception cref="NotImplementedException"></exception>
    public virtual void Create() { throw new NotImplementedException(); }

    /// <summary>
    /// Open a connection to the database.
    /// 
    /// This method must be overwritten in the concrete database class.
    /// </summary>
    /// <exception cref="NotImplementedException"></exception>
    public virtual IConnection GetConnection() { throw new NotImplementedException(); }

    /// <summary>
    /// Database credentials for normal connections
    /// </summary>
    public void SetCredentials(Tuple<string, string> credentials) { throw new NotImplementedException(); }

    /// <summary>
    /// Database credentials for administrative connections
    /// </summary>
    public void SetAdminCredentials(Tuple<string, string> credentials) { throw new NotImplementedException(); }
}