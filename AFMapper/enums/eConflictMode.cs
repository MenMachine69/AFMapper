
namespace AFMapper;

/// <summary>
/// Handling conflicts when saving into a database
/// </summary>
public enum eConflictMode
{
    /// <summary>
    /// first one wins, further changes are rejected
    /// </summary>
    FirstWins,
    /// <summary>
    /// the last one wins - changes are never rejected
    /// </summary>
    LastWins
}