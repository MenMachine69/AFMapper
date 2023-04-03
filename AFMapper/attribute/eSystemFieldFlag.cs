
namespace AFMapper;

/// <summary>
/// Flags for Fields to identify system relevant fields
/// </summary>
public enum eSystemFieldFlag
{
    /// <summary>
    /// PrimaryKey
    /// </summary>
    PrimaryKey,

    /// <summary>
    /// Timestamp created
    /// </summary>
    TimestampCreated,

    /// <summary>
    /// Timestamp changed
    /// </summary>
    TimestampChanged,

    /// <summary>
    /// Archived
    /// </summary>
    ArchiveFlag,

    /// <summary>
    /// Akk other fields
    /// </summary>
    None
}