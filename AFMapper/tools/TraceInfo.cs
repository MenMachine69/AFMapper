
namespace AFMapper;

/// <summary>
/// Tracing Informationen
/// </summary>
public class TraceInfo
{
    /// <summary>
    /// Create tracing infos for the command
    /// </summary>
    /// <param name="command">executed command</param>
    public TraceInfo(string command)
    {
        CommandText = command;
        TimeStamp = DateTime.Now;
    }

    /// <summary>
    /// Executed command
    /// </summary>
    public string CommandText { get; init; }

    /// <summary>
    /// Parameters for the command (can be empty)
    /// </summary>
    public object[]? CommandParameters { get; set; }

    /// <summary>
    /// Timestamp for coammand execution
    /// </summary>
    public DateTime TimeStamp { get; set; }

    /// <summary>
    /// Timespan for the execution
    /// </summary>
    public TimeSpan TimeSpan { get; set; }
}