
namespace AFMapper;

/// <summary>
/// Scheme for naming tables, views and fields
/// 
/// This schema can be used e.g. to create tables and fields when 
/// the database has special naming conventions (like PostgeSQL - all lowercase). 
/// </summary>
public enum eDatabaseNamingScheme
{
    /// <summary>
    /// all table, view and field names have the same names like there properties in the model
    /// </summary>
    original,
    /// <summary>
    /// all table, view and field names have lowercase names
    /// </summary>
    lowercase,
    /// <summary>
    /// all table, view and field names have uppercase names
    /// </summary>
    uppercase
}