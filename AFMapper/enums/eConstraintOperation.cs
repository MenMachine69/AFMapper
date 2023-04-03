
namespace AFMapper;

/// <summary>
/// Foreign key constraints
/// </summary>
public enum eConstraintOperation
{
    /// <summary>
    /// NO ACTION/RESTRICT
    /// </summary>
    NoAction,

    /// <summary>
    /// CASCADE
    /// </summary>
    Cascade,

    /// <summary>
    /// SET DEFAULT
    /// </summary>
    SetDefault,

    /// <summary>
    /// SET NULL
    /// </summary>
    SetNull
}