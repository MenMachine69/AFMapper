
namespace AFMapper;

/// <summary>
/// Attribute that describes a field in the database
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class AFField : Attribute
{
    /// <summary>
    /// Constructor with SourceField parameter for simpler FieldDefinitions in view classes
    /// </summary>
    /// <param name="sourceField"></param>
    public AFField(string sourceField)
    {
        SourceField = sourceField;
    }

    /// <summary>
    /// Constructor
    /// </summary>
    public AFField() { }

    /// <summary>
    /// Type to which the field refers.
    /// 
    /// This type must be specified if constraints are used.
    /// </summary>
    public Type? ConstraintType { get; set; }

    /// <summary>
    /// ForeignKey constraint for update operations
    /// 
    /// Only useful in conjunction with 'References'.
    /// </summary>
    public eConstraintOperation ConstraintUpdate { get; set; } = eConstraintOperation.NoAction;

    /// <summary>
    /// ForeignKey constraint for delete operations
    /// 
    /// Only useful in conjunction with 'References'.
    /// </summary>
    public eConstraintOperation ConstraintDelete { get; set; } = eConstraintOperation.NoAction;

    /// <summary>
    /// Set a flag for this field indicating whether the field is a system relevant field.
    /// The default flag is eSystemFieldFlag.None. Each flag can only be set for one field.
    /// </summary>
    public eSystemFieldFlag SystemFieldFlag { get; set; } = eSystemFieldFlag.None;

    /// <summary>
    /// Maximum length of this field in the database (if the field is a string).
    /// 
    /// The default value is 100. Use -1 to define a string blob field.
    /// 
    /// If the field is not a string, this value is ignored.
    /// </summary>
    public int MaxLength { get; set; } = 100;

    /// <summary>
    /// Size of a block in blob fields (only relevant if the database supports such an option).
    /// Default size is 512. Use a larger block size if most of the blob data is larger.
    /// </summary>
    public int BlobBlockSize { get; set; } = 512;

    /// <summary>
    /// Automatically use data compression for the field.
    /// ZIP is used for compression.
    /// 
    /// Can only be used with blob, image and binary fields (ByteArray).
    /// </summary>
    public bool Compress { get; set; }

    /// <summary>
    /// Field is used in the search engine (SearchEngine) for search (=is searchable)
    /// </summary>
    public bool Searchable { get; set; }

    /// <summary>
    /// Make field searchable using SoundEx (fuzzy search in strings)
    /// </summary>
    public bool UseSoundExSearch { get; set; }

    /// <summary>
    /// Marks a field as loaded with a delay (the first time the content is accessed).
    /// 
    /// Use this option for blob, image or binary fields that are not needed immediately, 
    /// but are e.g. opened/displayed in a viewer with a click of the user.
    /// </summary>
    public bool Delayed { get; set; }

    /// <summary>
    /// Specifies that an index is to be created for the field. In the index definition, if necessary. 
    /// the index can be further specified
    /// </summary>
    public bool Indexed { get; set; }

    /// <summary>
    /// Definition of the index (empty = only the actual field)
    /// </summary>
    public string? IndexDefinition { get; set; }

    /// <summary>
    /// Marks a field that can only contain unique values. 
    /// Any attempt to store a non-unique value in the field will result in an error.
    /// 
    /// Can only be used in combination with Indexed = true. If no index is created, the option is ignored!
    /// </summary>
    public bool Unique { get; set; }

    /// <summary>
    /// Name of the field in the view query.
    /// </summary>
    public string SourceField { get; set; } = string.Empty;

    /// <summary>
    /// Log changes. Only relevant if change logging is active 
    /// and logging is switched on in the table.
    /// </summary>
    public bool LogChanges { get; set; }

}