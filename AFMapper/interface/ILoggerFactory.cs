
namespace AFMapper;

/// <summary>
/// interface to create a new logger for database changes
/// </summary>
public interface ILoggerFactory
{
    /// <summary>
    /// Create a new logger for a connection
    /// </summary>
    /// <param name="db">database</param>
    /// <param name="conn">connection</param>
    /// <returns>the new ILogger</returns>
    ILogger GetLogger(IDatabase db, IConnection conn);
}