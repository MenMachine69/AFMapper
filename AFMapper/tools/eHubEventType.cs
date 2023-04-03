namespace AFMapper;

/// <summary>
/// Type of notification
/// </summary>
public enum eHubEventType
{
    /// <summary>
    /// New model added
    /// </summary>
    ObjectAdded = 0,

    /// <summary>
    /// Model changed
    /// </summary>
    ObjectChanged = 1,

    /// <summary>
    /// Model deleted
    /// </summary>
    ObjectDeleted = 2,

    /// <summary>
    /// Custom, non-specific message
    /// </summary>
    Custom = 3
}

