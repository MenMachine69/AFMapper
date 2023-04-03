
using System.ComponentModel;
using System.Data.Common;

namespace AFMapper;

/// <summary>
/// Interface for a database connection.
/// </summary>
public interface IConnection : IDisposable
{
    /// <summary>
    /// Suppress events via CR3.MessageHub
    /// </summary>
    bool Silent { get; set; }

    /// <summary>
    /// Read a single object from a reader
    /// </summary>
    /// <typeparam name="T">type of the object (table or view type)</typeparam>
    /// <param name="reader">reader to read the object from</param>
    /// <param name="dict">properties to read</param>
    /// <returns>the read object or NULL</returns>
    T? ReadFromReader<T>(DbDataReader reader, PropertyDescription[] dict) where T : IDataObject, new();

    /// <summary>
    /// Checks whether a value is unique
    /// </summary>
    /// <typeparam name="T">Type that contains the property to check</typeparam>
    /// <param name="data">object to check</param>
    /// <param name="field">property/field name to check</param>
    /// <param name="value">value to check</param>
    /// <returns>true = unique, otherwise false</returns>
    bool IsUnique<T>(T data, string field, object value) where T : ITable;

    /// <summary>
    /// Creates a new Transaction...
    /// </summary>
    void BeginTransaction();

    /// <summary>
    /// Commit changes for a existing transaction
    /// </summary>
    void CommitTransaction();

    /// <summary>
    /// Rollback changes for a existing transaction
    /// </summary>
    void RollbackTransaction();

    /// <summary>
    /// Checks if table or view exists and verify that structure is valid
    /// </summary>
    /// <typeparam name="T">Type</typeparam>
    void Check<T>() where T : IDataObject;

    /// <summary>
    /// Checks if table or view exists and verify that structure is valid
    /// </summary>
    /// <param name="type">Type</param>
    void Check(Type type);

    /// <summary>
    /// Checks if table or view exists and verify that structure is valid
    /// </summary>
    /// <typeparam name="T">Type</typeparam>
    /// <param name="force">force complete check</param>
    void Check<T>(bool force) where T : IDataObject;

    /// <summary>
    /// Checks if table or view exists and verify that structure is valid
    /// </summary>
    /// <param name="type">Type</param>
    /// <param name="force">force complete check</param>
    void Check(Type type, bool force);

    /// <summary>
    /// Load value of a single field from table
    /// </summary>
    /// <typeparam name="T">Type of the field</typeparam>
    /// <param name="tdesc">Table/View description</param>
    /// <param name="id">ID of the record/object from where the value should be load</param>
    /// <param name="fieldname">name of the field/column</param>
    /// <returns>value of this field</returns>
    T? LoadValue<T>(Type tdesc, Guid id, string fieldname);

    /// <summary>
    /// Select multiple objects from table or view
    /// </summary>
    /// <typeparam name="T">Type of the objects</typeparam>
    /// <param name="options">Options for reading</param>
    /// <param name="where">WHERE clause to read. Use ? as a placeholder for a parameter/argument.
    /// The clause can contain only the WHERE clause of the query or a complete SQL query
    /// (including SELECT and more)</param>
    /// <param name="args">arguments/parameters for the WHERE clause</param>
    /// <returns>BindingList with read objects or a empty BindingList</returns>
    BindingList<T> Select<T>(ReadOptions options, string where, params object[] args) where T : IDataObject, new();

    /// <summary>
    /// Select multiple objects from table or view
    /// </summary>
    /// <typeparam name="T">Type of the objects</typeparam>
    /// <param name="options">Options for reading</param>
    /// <returns>BindingList with read objects or a empty BindingList</returns>
    BindingList<T> Select<T>(ReadOptions options) where T : IDataObject, new();

    /// <summary>
    /// Select multiple objects from table or view
    /// </summary>
    /// <typeparam name="T">Type of the objects</typeparam>
    /// <param name="where">WHERE clause to read. Use ? as a placeholder for a parameter/argument.
    /// The clause can contain only the WHERE clause of the query or a complete SQL query
    /// (including SELECT and more)</param>
    /// <param name="args">arguments/parameters for the WHERE clause</param>
    /// <returns>BindingList with read objects or a empty BindingList</returns>
    BindingList<T> Select<T>(string where, params object[] args) where T : IDataObject, new();


    /// <summary>
    /// Select all objects from table or view
    /// </summary>
    /// <typeparam name="T">Type of the objects</typeparam>
    /// <returns>BindingList with all read objects or a empty BindingList if table or view is empty</returns>
    BindingList<T> Select<T>() where T : IDataObject, new();

    /// <summary>
    /// Select all objects from table or view and return a forward reader for the selected objects.
    /// This forward reader allows to read object by object including cancelation.
    /// </summary>
    /// <typeparam name="T">Type of the objects</typeparam>
    /// <returns>Reader for all objects in a table or view</returns>
    IReader<T> SelectReader<T>() where T : IDataObject, new();

    /// <summary>
    /// Select multiple objects from table or view and return a forward reader for the selected objects.
    /// This forward reader allows to read object by object including cancelation.
    /// </summary>
    /// <typeparam name="T">Type of the objects</typeparam>
    /// <param name="where">WHERE clause to read. Use ? as a placeholder for a parameter/argument.
    /// The clause can contain only the WHERE clause of the query or a complete SQL query
    /// (including SELECT and more)</param>
    /// <param name="args">arguments/parameters for the WHERE clause</param>
    /// <returns>Reader for multiple objects in a table or view</returns>
    IReader<T> SelectReader<T>(string where, params object[] args) where T : IDataObject, new();



    /// <summary>
    /// Select multiple objects from table or view and return a forward reader for the selected objects.
    /// This forward reader allows to read object by object including cancelation.
    /// </summary>
    /// <typeparam name="T">Type of the objects</typeparam>
    /// <param name="options">Options for reading</param>
    /// <param name="where">WHERE clause to read. Use ? as a placeholder for a parameter/argument.
    /// The clause can contain only the WHERE clause of the query or a complete SQL query
    /// (including SELECT and more)</param>
    /// <param name="args">arguments/parameters for the WHERE clause</param>
    /// <returns>Reader for multiple objects in a table or view</returns>
    IReader<T> SelectReader<T>(ReadOptions options, string where, params object[] args) where T : IDataObject, new();

    /// <summary>
    /// Select a single/the first object from table or view 
    /// </summary>
    /// <typeparam name="T">Type of the object</typeparam>
    /// <param name="where">WHERE clause to read. Use ? as a placeholder for a parameter/argument.
    /// The clause can contain only the WHERE clause of the query or a complete SQL query
    /// (including SELECT and more)</param>
    /// <param name="args">arguments/parameters for the WHERE clause</param>
    /// <returns>the read object or NULL if nothing found</returns>
    T? SelectSingle<T>(string where, params object[] args) where T : IDataObject, new();


    /// <summary>
    /// Select a single/the first object from table or view 
    /// </summary>
    /// <typeparam name="T">Type of the object</typeparam>
    /// <param name="options">Options for reading</param>
    /// <param name="where">WHERE clause to read. Use ? as a placeholder for a parameter/argument.
    /// The clause can contain only the WHERE clause of the query or a complete SQL query
    /// (including SELECT and more)</param>
    /// <param name="args">arguments/parameters for the WHERE clause</param>
    /// <returns>the read object or NULL if nothing found</returns>
    T? SelectSingle<T>(ReadOptions options, string where, params object[] args) where T : IDataObject, new();

    /// <summary>
    /// Load a single object from table or view on the basis of its PrimaryKey
    /// </summary>
    /// <typeparam name="T">Type of the object</typeparam>
    /// <param name="guid">PrimaryKey of the object</param>
    /// <returns>the read object or NULL if nothing found</returns>
    T? Load<T>(Guid guid) where T : IDataObject, new();

    /// <summary>
    /// Check if a table contains a object with the give PrimaryKey
    /// </summary>
    /// <typeparam name="T">Type of the object</typeparam>
    /// <param name="guid">PrimaryKey of the object</param>
    /// <returns>true if a object exist, otherwise false</returns>
    bool Exist<T>(Guid guid) where T : ITable;

    /// <summary>
    /// Save a object to the table in database and store/update only some fields
    /// </summary>
    /// <typeparam name="T">Type of the object</typeparam>
    /// <param name="record">the object to save</param>
    /// <param name="fields">fields to save/update</param>
    /// <returns>true if successful, otherwise false</returns>
    bool Save<T>(T record, string[]? fields = null) where T : class, ITable, IDataObject;

    /// <summary>
    /// Save a object to the table in database and store/update only some fields
    /// </summary>
    /// <typeparam name="T">Type of the object</typeparam>
    /// <param name="options">options for saving</param>
    /// <param name="record">the object to save</param>
    /// <returns>true if successful, otherwise false</returns>
    bool Save<T>(ReadOptions options, T record) where T : class, ITable, IDataObject;

    /// <summary>
    /// Save multiple objects to the table in database
    /// </summary>
    /// <typeparam name="T">Type of the object</typeparam>
    /// <param name="records">list of objects to save</param>
    /// <param name="fields">fields to save/update</param>
    /// <returns>number of saved objects</returns>
    int Save<T>(IEnumerable<T> records, string[]? fields = null) where T : class, ITable, IDataObject;

    /// <summary>
    /// Save multiple objects to the table in database
    /// </summary>
    /// <typeparam name="T">Type of the object</typeparam>
    /// <param name="options">options for saving</param>
    /// <param name="records">list of objects to save</param>
    /// <returns>number of saved objects</returns>
    int Save<T>(ReadOptions options, IEnumerable<T> records) where T : class, ITable, IDataObject;

    /// <summary>
    /// Delete a single record
    /// </summary>
    /// <typeparam name="T">Type of table</typeparam>
    /// <param name="record">the record to delete</param>
    /// <returns>true if deleted, otherwise false</returns>
    bool Delete<T>(T record) where T : class, ITable;

    /// <summary>
    /// Delete a single record by its PrimaryKey
    /// </summary>
    /// <typeparam name="T">Type of records</typeparam>
    /// <param name="id">PrimaryKey of the record to delete</param>
    /// <returns>true if deleted, otherwise false</returns>
    bool Delete<T>(Guid id) where T : class, ITable, new();

    /// <summary>
    /// Delete multiple records
    /// </summary>
    /// <typeparam name="T">Type of table</typeparam>
    /// <param name="records">List of records to delete</param>
    /// <returns>count of deleted records</returns>
    int Delete<T>(IEnumerable<T> records) where T : class, ITable;

    /// <summary>
    /// Count the number of records in a table or view
    /// </summary>
    /// <typeparam name="T">Type of table or view</typeparam>
    /// <param name="options">options</param>
    /// <param name="query">query (where clause)</param>
    /// <param name="args">arguments for the query</param>
    /// <returns>count of records</returns>
    int Count<T>(ReadOptions options, string query, params object[] args) where T : IDataObject;

    /// <summary>
    /// Count the number of records in a table or view
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="query"></param>
    /// <param name="args"></param>
    /// <returns></returns>
    int Count<T>(string query, params object[] args) where T : IDataObject;

    /// <summary>
    /// Count the number of records in a table or view
    /// </summary>
    /// <typeparam name="T">Type of table or view</typeparam>
    /// <returns>Number of records in table or view</returns>
    int Count<T>() where T : IDataObject;

    /// <summary>
    /// retrieve the sum of a field
    /// </summary>
    /// <typeparam name="T">Table tppe</typeparam>
    /// <param name="options">options for query</param>
    /// <param name="field">name of the field</param>
    /// <param name="query">query (where clause)</param>
    /// <param name="args">arguments for the query</param>
    /// <returns>the sum </returns>
    decimal Sum<T>(ReadOptions options, string field, string query, params object[] args) where T : IDataObject;

    /// <summary>
    /// retrieve the sum of a field
    /// </summary>
    /// <typeparam name="T">Type of table or view</typeparam>
    /// <param name="field">name of the field</param>
    /// <param name="query">query (where clause)</param>
    /// <param name="args">arguments for the query</param>
    /// <returns>the sum </returns>
    decimal Sum<T>(string field, string query, params object[] args) where T : IDataObject;

    /// <summary>
    /// retrieve the sum of a field
    /// </summary>
    /// <typeparam name="T">Type of table or view</typeparam>
    /// <param name="field">name of the field</param>
    /// <returns>the sum </returns>
    decimal Sum<T>(string field) where T : IDataObject;

    /// <summary>
    /// execute a stored procedure that returns a single value
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="procedureName"></param>
    /// <param name="args"></param>
    /// <returns></returns>
    T? ExecuteProcedureScalar<T>(string procedureName, params object[] args);

    /// <summary>
    /// execute a stored procedure and return the number of affected rows
    /// </summary>
    /// <param name="procedureName">name of the procedure</param>
    /// <param name="args">arguments for execution</param>
    /// <returns>number of affected rows</returns>
    int ExecuteProcedure(string procedureName, params object[] args);

    /// <summary>
    /// execute a sql command that returns a single value
    /// row in the result set returned by the query.
    /// Additional columns or rows are ignored.
    /// </summary>
    /// <typeparam name="T">return type</typeparam>
    /// <param name="commandQuery">sql query</param>
    /// <param name="args">arguments for the query</param>
    /// <returns>the value or null</returns>
    T? ExecuteCommandScalar<T>(string commandQuery, params object[] args);

    /// <summary>
    /// execute a sql command and return the number of affected rows
    /// </summary>
    /// <param name="commandQuery">sql command to execute</param>
    /// <param name="args">arguments for the query</param>
    /// <returns>number of affected rows</returns>
    int ExecuteCommand(string commandQuery, params object[] args);
}