
namespace AFMapper;

/// <summary>
/// Interface für einen Forward-Reader einer Datenbank
/// </summary>
public interface IReader<out T> : IDisposable where T : IDataObject, new()
{
    /// <summary>
    /// reading on object/record from reader
    /// </summary>
    /// <returns>the read object/record</returns>
    T? Read();

    /// <summary>
    /// close the reader
    /// </summary>
    void Close();

    /// <summary>
    /// Delivers true when the Reader has reached the end....
    /// </summary>
    bool Eof { get; }
}