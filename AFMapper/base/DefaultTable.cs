 
namespace AFMapper;

/// <summary>
/// Base class for Tables that contains default fields:
/// 
/// SYS_ID          Unique Key
/// SYS_CREATED     Timestamp created
/// SYS_CHANGED     Timestamp last changed
/// SYS_ARCHIVED    Flag: is archived
///
/// Use this class for tables that should contain this default fields.
/// </summary>
public abstract class DefaultTable : BaseTable
{
    private Guid _SYS_ID = Guid.Empty;
    private DateTime _SYS_CREATED = DateTime.MinValue;
    private DateTime _SYS_CHANGED = DateTime.MinValue;
    private bool _SYS_ARCHIVED;

    /// <summary>
    /// Primary key of the object.
    /// </summary>
    public override Guid PrimaryKey 
    { 
        get => SYS_ID;
        set => SYS_ID = value;
    }


    /// <summary>
    /// DateTime created
    /// </summary>
    public override DateTime CreateDateTime
    {
        get => SYS_CREATED;
        set => SYS_CREATED = value;
    }

    /// <summary>
    /// DateTime last changed
    /// </summary>
    public override DateTime UpdateDateTime
    {
        get => SYS_CHANGED;
        set => SYS_CHANGED = value;
    }

    /// <summary>
    /// Marks the object as archived
    /// </summary>
    public override bool IsArchived
    {
        get => SYS_ARCHIVED;
        set => SYS_ARCHIVED = value;
    }

    /// <summary>
    /// Primary key of the object.
    /// </summary>
    [CRField(SystemFieldFlag = eSystemFieldFlag.PrimaryKey)]
    public Guid SYS_ID
    {
        get => _SYS_ID;
        set => Set(ref _SYS_ID, value);
    }

    /// <summary>
    /// DateTime created
    /// </summary>
    [CRField(SystemFieldFlag = eSystemFieldFlag.TimestampCreated)]
    public DateTime SYS_CREATED
    {
        get => _SYS_CREATED;
        set => Set(ref _SYS_CREATED, value);
    }

    /// <summary>
    /// DateTime last changed
    /// </summary>
    [CRField(SystemFieldFlag = eSystemFieldFlag.TimestampChanged)]
    public DateTime SYS_CHANGED
    {
        get => _SYS_CHANGED;
        set => Set(ref _SYS_CHANGED, value);
    }

    /// <summary>
    /// Marks the object as archived
    ///
    /// ATTENTION! CRBindung is defined with readonly. Do not edit this value in masks directly.
    /// Instead of direct edition set this value from a command or something else.
    /// </summary>
    [CRField(SystemFieldFlag = eSystemFieldFlag.ArchiveFlag)]
    public bool SYS_ARCHIVED
    {
        get => _SYS_ARCHIVED;
        set => Set(ref _SYS_ARCHIVED, value);
    }
}