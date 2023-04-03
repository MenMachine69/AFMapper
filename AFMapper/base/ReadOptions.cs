 
namespace AFMapper;

/// <summary>
/// Options for reading and writing data
/// </summary>
public sealed class ReadOptions
{
    /// <summary>
    /// allways create without checking for exist
    /// </summary>
    public bool ForceCreate { get; set; }

    /// <summary>
    /// order data by (SQL: ORDER BY) 
    /// </summary>
    public string? OrderBy { get; set; }

    /// <summary>
    /// group data on (SQL: GROUP BY)
    /// </summary>
    public string? GroupOn { get; set; }

    /// <summary>
    /// Fields to be read (empty = all)
    /// 
    /// If this option is empty, all fields are read from the source.
    /// 
    /// The option can be used to limit the fields to be read to those that are
    /// required in a particular context, which can have a significant impact 
    /// on the loading time.
    /// </summary>
    public string[] Fields { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Maximum number of dataobjects to be read
    /// </summary>
    public int MaximumRecordCount { get; set; }

    /// <summary>
    /// order mode (ascending or descending)
    /// </summary>
    public eOrderMode OrderMode { get; set; }

    /// <summary>
    /// Read fields marked as Delayed immediately (true)
    /// </summary>
    public bool IgnoreDelayed { get; set; }

    /// <summary>
    /// A filter function that is called for each loaded data object.
    /// 
    /// If this function returns false for the specified data object, that data object. 
    /// it will not be added to the result.
    ///
    /// This filter function is only used with Select-Method in IConnection.
    /// </summary>
    public Func<object, bool>? Filter { get; set; }

    /// <summary>
    /// If true, all fields are written to the database, even if they have not been changed.
    /// The default is false. Only changed fields are written.
    /// This option is only used with Save-Method in IConnection.
    /// </summary>
    public bool WriteAllFields { get; set; }
}