
namespace AFMapper;

/// <summary>
/// Attribute that describes a view in the database.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class AFView : Attribute
{
    /// <summary>
    /// Name of the view in the database.
    /// If no name is given, the name of the view corresponds to the name of the type preceded by VW_.
    /// 
    /// Example: Type: FirmaAktivitaet
    ///          View name: VW_FIRMAAKTIVITAET (Notation depending on database configuration - Large, Small or Mixed)
    /// 
    /// </summary>
    public string ViewName { get; set; } = "";

    /// <summary>
    /// Query for the view
    /// </summary>
    public string Query { get; set; } = "";

    /// <summary>
    /// Database version with which the view was/is to be adjusted the last time.
    ///
    /// If the version is increased, the structure of the view is checked when the database is checked.
    /// </summary>
    public int Version { get; set; }

    /// <summary>
    /// Internal ID of the table
    /// 
    /// IDs from 0 - 100 are reserved for CR3 internal things. For own tables use only IDs &gt; 100!
    /// </summary>
    public int ViewId { get; set; }

    /// <summary>
    /// Use cache for the data (data is cached if possible and read from the cache instead of the database).
    /// </summary>
    public bool UseCache { get; set; }


    /// <summary>
    /// Type of the Master/Table for this view.
    /// 
    /// Master must be the primary Type of the primnary Table for this view (select ... from 'master' ...). 
    /// This type is needed to detect the correct IController for this view.
    /// </summary>
    public Type MasterType { get; set; } = typeof(Nullable);
}