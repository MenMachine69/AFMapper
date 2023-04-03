namespace AFMapper;

/// <summary>
/// Token describing a subscription
/// </summary>
public class EventToken : IDisposable
{
    private readonly WeakReference _hubRef;


    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="hub">Hub that creates and manages the token</param>.
    /// <param name="messageType">Type of object for which messages are to be transmitted</param>.
    public EventToken(EventHub hub, Type messageType)
    {
        _hubRef = hub != null ? new(hub) : throw new ArgumentException(nameof(hub));
        MessageType = messageType ?? throw new ArgumentException(nameof(messageType));
    }

    /// <summary>
    /// Type of object for which messages are to be transmitted
    /// </summary>
    public Type MessageType { get; }

    /// <summary>
    /// Clear token - also removes subscription...
    /// </summary>
    public void Dispose()
    {
        if (!_hubRef.IsAlive) return;

        EventHub? hub = _hubRef.Target as EventHub;

        hub?.Unsubscribe(this);
    }
}