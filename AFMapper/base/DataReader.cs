using System.Data.Common;

namespace AFMapper;

/// <summary>
/// A forward reader for a database that allows record-by-record editing....
/// </summary>
public class DataReader<T> : IReader<T> where T : IDataObject, new()
{
    private readonly DbDataReader _reader;
    private readonly IConnection _connection;
    private readonly PropertyDescription[] _dict;


    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="reader">Reader from which to read</param>
    /// <param name="connection">Connection used by the reader</param>
    public DataReader(DbDataReader reader, IConnection connection)
    {
        _connection = connection;
        _reader = reader;

        var cols = _reader.FieldCount;
        _dict = new PropertyDescription[cols];

        for (int i = 0; i < cols; i++)
        {
            string fieldname = reader.GetName(i);

            var fld = typeof(T).GetTypeDescription().Fields.Values.FirstOrDefault(f => f.Name.ToLower() == fieldname.ToLower());

            if (fld != null)
                _dict[i] = fld;
        }

        if (reader.HasRows)
            Eof = !reader.Read();
        else
            Eof = true;
    }

    /// <summary>
    /// Reads an object from the reader
    /// </summary>
    /// <returns>the read object or NULL</returns>
    public T? Read()
    {
        if (Eof)
            return default(T);

        T? ret = _connection.ReadFromReader<T>(_reader, _dict);

        Eof = !_reader.Read();

        return ret;
    }

    /// <summary>
    /// Close the reader and clean up.
    /// 
    /// THE CONNECTION IS NOT CLOSED! This must be done in the calling code.
    /// </summary>
    public void Close()
    {
        _reader.Close();
        _reader.Dispose();
    }

    /// <summary>
    /// true when the reader has reached the end
    /// </summary>
    public bool Eof { get; private set; }

    /// <summary>
    /// clean-up
    /// </summary>
    public void Dispose()
    {
        Close();
    }
}
