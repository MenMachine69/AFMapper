
namespace AFMapper;

/// <summary>
/// Translator for a database dialect
/// </summary>
public interface ITranslator
{
    /// <summary>
    /// Convert value from database to target type
    /// </summary>
    /// <param name="value">value</param>
    /// <param name="targettype">convert to type</param>
    /// <returns>converted value</returns>
    object? FromDatabase(object? value, Type targettype);

    /// <summary>
    /// Convert value to database type
    /// </summary>
    /// <param name="value">value to convert</param>
    /// <returns></returns>
    object ToDatabase(object value);

    /// <summary>
    /// Convert value to database type
    /// </summary>
    /// <param name="value">value to convert</param>
    /// <param name="valuetype">taregt type</param>
    /// <returns>converted value</returns>
    object ToDatabase(object value, Type valuetype);

    /// <summary>
    /// Convert value to database type with optional compression if value is bytearray or a serializable object
    /// </summary>
    /// <param name="value">value to convert</param>
    /// <param name="valuetype">taregt type</param>
    /// <param name="compress">compress data (ZIP)</param>
    /// <returns>converted value</returns>
    object ToDatabase(object value, Type valuetype, bool compress);

    /// <summary>
    /// get command string for a specific coammand/element
    /// </summary>
    /// <param name="command">command/element</param>
    /// <returns>code for the command/element</returns>
    string GetCommandString(eCommandString command);

    /// <summary>
    /// translate a sql query for the database
    /// </summary>
    /// <param name="query">query</param>
    /// <returns>translated query</returns>
    string TranslateQuery(ref string query);

    /// <summary>
    /// get sql code for a specific trigger event
    /// </summary>
    /// <param name="code">trigger</param>
    /// <returns>code for this trigger</returns>
    string GetTriggerEvent(eTriggerEvent code);
}