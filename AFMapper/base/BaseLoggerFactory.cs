
namespace AFMapper;

/// <summary>
/// Factory for creating new loggers
/// </summary>
public class BaseLoggerFactory : ILoggerFactory
{
    /// <summary>
    /// Create a new logger
    /// </summary>
    /// <param name="db">database</param>
    /// <param name="conn">connection</param>
    /// <returns>a new logger</returns>
    public ILogger GetLogger(IDatabase db, IConnection conn)
    {
        return new BaseLogger() { Output = Output };
    }

    /// <summary>
    /// Output action.
    /// 
    /// This delegate will be executed for every locked change.
    /// </summary>
    public Action<Tuple<Guid, string, object?, object?>>? Output { get; set; }
}

/// <summary>
/// simple logger for database changes (ILogger)
/// 
/// assign a action delegate to Output which will be executed for every change.
/// </summary>
public class BaseLogger : ILogger
{
    private readonly List<Tuple<Guid, string, object?, object?>> _cache = new();
    private bool _inTransaction;

    /// <summary>
    /// begin a transaction
    /// 
    /// Inside a transaction all changes will be buffered until CommitTransaction is executed.
    /// </summary>
    public void BeginTransaction()
    {
        _cache.Clear();
        _inTransaction = true;
    }

    /// <summary>
    /// Commit changes from inside a transaction
    /// 
    /// For every buffered change Output will be executed.
    /// </summary>
    public void CommitTransaction()
    {
        if (_cache.Count > 0)
            _cache.ForEach(e => Output?.Invoke(e));


        _cache.Clear();
        _inTransaction = false;
    }

    /// <summary>
    /// Log a change
    /// </summary>
    /// <param name="recordID">record ID (primary key)</param>
    /// <param name="field">changed field</param>
    /// <param name="oldvalue">oldvalue</param>
    /// <param name="newvalue">new value</param>
    public void Log(Guid recordID, string field, object? oldvalue, object? newvalue)
    {
        if (_inTransaction)
            _cache.Add(new(recordID, field, oldvalue, newvalue));
        else
            Output?.Invoke(new(recordID, field, oldvalue, newvalue));
    }

    /// <summary>
    /// rollback all changes
    /// 
    /// No change from inside a transaction will be submitted to Output.
    /// </summary>
    public void RollbackTransaction()
    {
        _cache.Clear();
        _inTransaction = false;

    }

    /// <summary>
    /// Output action.
    /// 
    /// This delegate will be executed for every locked change.
    /// </summary>
    public Action<Tuple<Guid, string, object?, object?>>? Output { get; set; }
}