
namespace AFMapper;

/// <summary>
/// interface for a logger that can log changes in databases
/// 
/// each logger will be log changes in a specific connection
/// </summary>
public interface ILogger
{
    /// <summary>
    /// start a transaction (changes will be buffered until CommitTransaction)
    /// </summary>
    void BeginTransaction();

    /// <summary>
    /// commit changes in current transaction (write changes)
    /// </summary>
    void CommitTransaction();

    /// <summary>
    /// rollback changes in current transaction (dismiss changes)
    /// </summary>
    void RollbackTransaction();

    /// <summary>
    /// log a change
    /// </summary>
    /// <param name="recordID">ID of record</param>
    /// <param name="field">name of field</param>
    /// <param name="oldvalue">old value</param>
    /// <param name="newvalue">new value</param>
    void Log(Guid recordID, string field, object? oldvalue, object? newvalue);
}