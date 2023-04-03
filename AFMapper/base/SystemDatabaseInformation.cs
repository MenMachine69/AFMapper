
namespace AFMapper;

/// <summary>
/// Model for general informations about the database
/// </summary>
[CRTable(TableName = "SYS_INFO", TableId = 1, Version = 1)]
public class SystemDatabaseInformation : DefaultTable
{
    private int _SYSINFO_DBVERSION;
    private int _SYSINFO_IDENTIFIER;
    private bool _SYSINFO_MAINTENANCE;
    private string _SYSINFO_TABLENAME = "";

    /// <summary>
    /// database/table version
    /// </summary>
    [CRField()]
    public int SYSINFO_DBVERSION
    {
        get => _SYSINFO_DBVERSION;
        set => Set(ref _SYSINFO_DBVERSION, value);
    }

    /// <summary>
    /// entity identifier - unque id of the table/view
    /// </summary>
    [CRField(Unique = true, Indexed = true)]
    public int SYSINFO_IDENTIFIER
    {
        get => _SYSINFO_IDENTIFIER;
        set => Set(ref _SYSINFO_IDENTIFIER, value);
    }

    /// <summary>
    /// Informations about the maintenance state of the database
    /// </summary>
    [CRField()]
    public bool SYSINFO_MAINTENANCE
    {
        get => _SYSINFO_MAINTENANCE;
        set => Set(ref _SYSINFO_MAINTENANCE, value);
    }

    /// <summary>
    /// Table name - if this field is empty, the object contains informations about the database itself. 
    /// </summary>
    [CRField(MaxLength = 200)]
    public string SYSINFO_TABLENAME
    {
        get => _SYSINFO_TABLENAME;
        set => Set(ref _SYSINFO_TABLENAME, value);
    }
}