
using CR3.CORE;
using System.ComponentModel;
using System.Data.Common;
using System.Data;
using System.Drawing;
using System.Reflection;
using System.Text;

namespace AFMapper;

/// <summary>
/// Base class for Connection classes which encapsulate 
/// shared properties and methods for all these classes.
/// </summary>
public class BaseConnection<TConnection, TCommand, TParameter, TTransaction> : IConnection 
    where TConnection : DbConnection, new() 
    where TCommand : DbCommand, new() 
    where TParameter : DbParameter, new() 
    where TTransaction : DbTransaction
{
    private readonly List<Tuple<IDataObject, eHubEventType>> _msgBuffer = new();
    private readonly object msgBufferLock = new();
    private ILogger? _logger;

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="database">Database for this connection</param>
    public BaseConnection(IDatabase database)
    {
        Database = database;
    }

    /// <summary>
    /// Database for this connection
    /// </summary>
    public IDatabase Database { get; init; }

    /// <summary>
    /// Access to current inner connection
    /// </summary>
    public TConnection? Connection { get; set; }

    /// <summary>
    /// a logger for changes
    /// </summary>
    public ILogger? ChangeLogger
    {
        get
        {
            if (_logger == null && Database.LoggerFactory != null)
                _logger = Database.LoggerFactory.GetLogger(Database, this);
            return _logger;
        }
    }

    /// <summary>
    /// Ereignisse via Core.MessageHub unterdrücken
    /// </summary>
    public bool Silent { get; set; } = false;

    #region Transactions
    /// <summary>
    /// Access to current transaction
    /// </summary>
    public TTransaction? Transaction { get; set; }

    /// <summary>
    /// Creates a new Transaction...
    /// </summary>
    public virtual void BeginTransaction()
    {
        if (Connection == null)
            throw new InvalidOperationException(Strings.ERROR_NOACTIVECONNECTION);

        if (Transaction != null)
            throw new InvalidOperationException(Strings.ERROR_DBTRANSACTIONEXISTS);

        if (ChangeLogger != null)
            ChangeLogger.BeginTransaction();

        Transaction = (TTransaction)Connection.BeginTransaction();
    }

    /// <summary>
    /// Commit changes for a existing transaction
    /// </summary>
    public void CommitTransaction()
    {
        if (Connection == null)
            throw new InvalidOperationException(Strings.ERROR_NOACTIVECONNECTION);

        if (Transaction == null)
            throw new InvalidOperationException(Strings.ERROR_NOACTIVETRANSACTION);

        try
        {
            Transaction.Commit();
            Transaction.Dispose();

            lock (msgBufferLock)
            {
                foreach (var msg in _msgBuffer) MapperCore.EventHub?.Deliver(msg.Item1, msg.Item2);

                _msgBuffer.Clear();
            }

            Transaction = null;

            if (ChangeLogger != null)
                ChangeLogger.CommitTransaction();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(Strings.ERROR_COMMITTRANSACTION, ex);
        }
    }

    /// <summary>
    /// Rollback changes for a existing transaction
    /// </summary>
    public void RollbackTransaction()
    {
        if (Connection == null)
            throw new InvalidOperationException(Strings.ERROR_NOACTIVECONNECTION);

        if (Transaction == null)
            throw new InvalidOperationException(Strings.ERROR_NOACTIVETRANSACTION);

        try
        {
            Transaction.Rollback();
            Transaction.Dispose();

            lock (msgBufferLock)
            {
                _msgBuffer.Clear();
            }

            Transaction = null;

            if (ChangeLogger != null)
                ChangeLogger.RollbackTransaction();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(Strings.ERROR_COMMITTRANSACTION, ex);
        }
    }
    #endregion

    #region execute commands or procedures
    /// <summary>
    /// Executes a stored procedure in the database that returns exactly one value.
    /// </summary>
    /// <typeparam name="T">return type</typeparam>
    /// <param name="procedureName">prcedure name</param>
    /// <param name="args">arguments for the procedure</param>
    /// <returns>value</returns>
    public T? ExecuteProcedureScalar<T>(string procedureName, params object[] args)
    {
        T? ret = default;

        string qry = _getCommand(eCommandString.ExecProcedure).Replace("#PROCEDURE#", Database.GetName(procedureName)).ToString();
        qry += args.Length > 0 ? ("(" + "#".PadRight(args.Length, '#').Replace("#", "?,") + ")").Replace("?,)", "?)") : "";

        using (TCommand cmd = _parseCommand(qry, args))
        {
            cmd.CommandType = CommandType.StoredProcedure;
            Database.TraceBeforeExecute?.Invoke(new TraceInfo(cmd.CommandText));
            object? data = Database.Translator.FromDatabase(cmd.ExecuteScalar(), typeof(T));

            if (data is T d)
                ret = d;

            Database.TraceAfterExecute?.Invoke(new TraceInfo(cmd.CommandText));
        }

        return ret;
    }

    /// <summary>
    /// Executes a stored procedure in the database.
    /// </summary>
    /// <param name="procedureName">prcedure name</param>
    /// <param name="args">arguments for the procedure</param>
    /// <returns>number of rows affected</returns>
    public int ExecuteProcedure(string procedureName, params object[] args)
    {
        int ret;

        string qry = _getCommand(eCommandString.ExecProcedure).Replace("#PROCEDURE#", Database.GetName(procedureName)).ToString();
        qry += args.Length > 0 ? ("(" + "#".PadRight(args.Length, '#').Replace("#", "?,") + ")").Replace("?,)", "?)") : "";

        using (TCommand cmd = _parseCommand(qry, args))
        {
            cmd.CommandType = CommandType.StoredProcedure;
            Database.TraceBeforeExecute?.Invoke(new TraceInfo(cmd.CommandText));
            ret = cmd.ExecuteNonQuery();
            Database.TraceAfterExecute?.Invoke(new TraceInfo(cmd.CommandText));
        }

        return ret;
    }

    /// <summary>
    /// Executes a sql command that returns exactly one value.
    /// </summary>
    /// <typeparam name="T">return type</typeparam>
    /// <param name="commandQuery">query/command</param>
    /// <param name="args">arguments for the command</param>
    /// <returns>value</returns>
    public T? ExecuteCommandScalar<T>(string commandQuery, params object[] args)
    {
        T? ret = default;

        using (TCommand cmd = _parseCommand(commandQuery, args))
        {
            cmd.CommandType = CommandType.Text;
            Database.TraceBeforeExecute?.Invoke(new TraceInfo(cmd.CommandText));

            object? data = Database.Translator.FromDatabase(cmd.ExecuteScalar(), typeof(T));

            if (data is T d)
                ret = d;

            Database.TraceAfterExecute?.Invoke(new TraceInfo(cmd.CommandText));
        }

        return ret;
    }

    /// <summary>
    /// Executes a command/query in the database.
    /// </summary>
    /// <param name="commandQuery">query/command</param>
    /// <param name="args">arguments for the command</param>
    /// <returns>number of rows affected</returns>
    public int ExecuteCommand(string commandQuery, params object[]? args)
    {
        int ret = 0;

        if (commandQuery.IsEmpty())
            return ret;

        using (TCommand cmd = _parseCommand(commandQuery, args))
        {
            cmd.CommandType = CommandType.Text;
            Database.TraceBeforeExecute?.Invoke(new TraceInfo(cmd.CommandText));
            ret = cmd.ExecuteNonQuery();
            Database.TraceAfterExecute?.Invoke(new TraceInfo(cmd.CommandText));
        }

        return ret;
    }
    #endregion

    #region create/modify/delete data
    /// <summary>
    /// Save a data object of type T
    /// </summary>
    /// <typeparam name="T">type of object to save</typeparam>
    /// <param name="data">object</param>
    /// <param name="options">query options</param>
    public bool Save<T>(ReadOptions options, T data) where T : class, ITable, IDataObject
    {
        data.BeforeSave();

        bool isNew = false;
        StringBuilder query = _getCommand(eCommandString.Update);
        List<TParameter> para = new();

        TypeDescription tdesc = data.GetType().GetTypeDescription();

        if (tdesc.Table == null)
            throw new Exception($"Type {tdesc.Type.FullName} is not a table.");

        if (tdesc.FieldKey == null)
            throw new Exception($"Type {tdesc.Type.FullName} does not contain field for primary key.");

        Guid keyvalue = (Guid)tdesc.Accessor[data, ((PropertyInfo)tdesc.FieldKey).Name];

        if (options.ForceCreate || keyvalue.IsEmpty() || Exist<T>(keyvalue) == false)
        {
            query = _getCommand(eCommandString.Insert);
            isNew = true;
        }
        else
        {
            if (Database.Configuration.ConflictMode == eConflictMode.FirstWins && tdesc.FieldChanged != null)
            {
                DateTime lastchanged = LoadValue<DateTime>(typeof(T), keyvalue, tdesc.FieldChanged.Name);

                if (lastchanged > (DateTime)tdesc.Accessor[data, ((PropertyInfo)tdesc.FieldChanged).Name])
                    throw new InvalidOperationException("Der Datensatz wurde zwischenzeitlich geändert.");
            }

            para.Add(new TParameter { ParameterName = "@v0", Value = Database.Translator.ToDatabase(keyvalue, typeof(Guid)) });
        }

        // save only changed fields... 
        if (options.Fields.Length < 1 && !options.WriteAllFields && !isNew)
        {
            options.Fields = data.ChangedProperties.Keys.ToArray();
            
            if (options.Fields.Length < 1)
                return true; // nothing to save, directly return true
        }

        int cnt = 1;

        StringBuilder sbFields = new();
        StringBuilder sbValues = new();
        StringBuilder sbPairs = new();


        foreach (PropertyDescription desc in tdesc.Fields.Values)
        {
            if (desc.Field == null)
                continue;

            if ((desc.Field.SystemFieldFlag == eSystemFieldFlag.TimestampChanged || desc.Field.SystemFieldFlag == eSystemFieldFlag.TimestampCreated) && options.ForceCreate == false)
                continue;

            if (desc.Field.Delayed != data.IsDelayedLoaded(((PropertyInfo)desc).Name))
                continue;

            if (desc.Field.SystemFieldFlag != eSystemFieldFlag.PrimaryKey && options.Fields.Length > 0 && options.Fields.Contains(((PropertyInfo)desc).Name) == false)
                continue;

            if (isNew == false && desc.Field.SystemFieldFlag == eSystemFieldFlag.PrimaryKey)
                continue;

            string varname = "@v" + cnt.ToString().Trim();

            if (cnt > 1)
            {
                sbFields.Append(", ");
                sbValues.Append(", ");
                sbPairs.Append(", ");
            }

            sbFields.Append(((PropertyInfo)desc).Name);
            sbValues.Append(varname);
            sbPairs.Append(((PropertyInfo)desc).Name);
            sbPairs.Append(" = ");
            sbPairs.Append(varname);

            TParameter paramater = new()
            {
                ParameterName = varname
            };

            if (desc.Field.SystemFieldFlag == eSystemFieldFlag.PrimaryKey && keyvalue.IsEmpty())
            {
                Guid newkey = Guid.NewGuid();
                tdesc.Accessor[data, ((PropertyInfo)desc).Name] = newkey;
                paramater.Value = Database.Translator.ToDatabase(newkey, typeof(Guid));
            }
            else
                paramater.Value = Database.Translator.ToDatabase(tdesc.Accessor[data, ((PropertyInfo)desc).Name], ((PropertyInfo)desc).PropertyType);

            para.Add(paramater);

            ++cnt;
        }

        query.Replace("#TABLENAME#", tdesc.Table.TableName)
            .Replace("#FIELDNAMEKEY#", ((PropertyInfo)tdesc.FieldKey).Name)
            .Replace("#FIELDS#", sbFields.ToString())
            .Replace("#VALUES#", sbValues.ToString())
            .Replace("#PAIRS#", sbPairs.ToString());


        TCommand cmd = _parseCommand(query.ToString(), null);
        cmd.CommandType = CommandType.Text;
        cmd.Parameters.AddRange(para.ToArray());

        Database.TraceBeforeExecute?.Invoke(new TraceInfo(cmd.CommandText));

        var ret = cmd.ExecuteNonQuery() > 0;

        if (Silent != true && tdesc.Table.LogChanges && data.ChangedProperties.Count > 0 && ChangeLogger != null)
        {
            foreach (var prop in data.ChangedProperties)
            {
                var property = tdesc.Properties[prop.Key];

                if (property.Field != null && property.Field.LogChanges)
                    ChangeLogger.Log(data.PrimaryKey, ((PropertyInfo)property).Name, prop.Value, tdesc.Accessor[data, ((PropertyInfo)property).Name]);
            }
        }

        data.CommitChanges();

        Database.TraceAfterExecute?.Invoke(new TraceInfo(cmd.CommandText));
        Database.AfterSave?.Invoke(data);


        if (Silent) return ret;

        if (Transaction == null)
            MapperCore.EventHub.Deliver(data, isNew ? eHubEventType.ObjectAdded : eHubEventType.ObjectChanged);
        else
        {
            lock (msgBufferLock)
            {
                _msgBuffer.Add(new Tuple<IDataObject, eHubEventType>(data, isNew ? eHubEventType.ObjectAdded : eHubEventType.ObjectChanged));
            }
        }

        return ret;
    }

    /// <summary>
    /// Save a list of data objects of type T
    /// </summary>
    /// <typeparam name="T">type of object to save</typeparam>
    /// <param name="data">list of objects</param>
    /// <param name="fields">save only this fields</param>
    public int Save<T>(IEnumerable<T> data, string[]? fields = null) where T : class, ITable, IDataObject
    {
        ReadOptions options = fields != null ? new() { Fields = fields } : new();

        return Save(options, data);
    }

    /// <summary>
    /// Save a list of data objects of type T
    /// </summary>
    /// <typeparam name="T">type of object to save</typeparam>
    /// <param name="data">list of objects</param>
    /// <param name="options">query options</param>
    public int Save<T>(ReadOptions options, IEnumerable<T> data) where T : class, ITable, IDataObject
    {
        int ret = 0;

        foreach (T entry in data)
        {
            if (Save(options, entry))
                ++ret;
        }

        return ret;
    }

    /// <summary>
    /// Save a list of data objects of type T
    /// </summary>
    /// <typeparam name="T">type of object to save</typeparam>
    /// <param name="data">object</param>
    /// <param name="fields">save only this fields</param>
    public bool Save<T>(T data, string[]? fields = null) where T : class, ITable, IDataObject
    {

        ReadOptions options = fields != null ? new() { Fields = fields } : new();
        return Save(options, data);
    }

    /// <summary>
    /// delete a data object of type T
    /// </summary>
    /// <typeparam name="T">type of object to save</typeparam>
    /// <param name="data">object to delete</param>
    public bool Delete<T>(T data) where T : class, ITable
    {
        bool ret = false;
        TypeDescription tdesc = data.GetType().GetTypeDescription();

        if (tdesc.Table == null)
            throw new Exception($"Type {tdesc.Type.FullName} is not a table.");

        if (tdesc.FieldKey == null)
            throw new Exception($"Type {tdesc.Type.FullName} does not contain field for primary key.");

        Guid keyvalue = (Guid)tdesc.Accessor[data, ((PropertyInfo)tdesc.FieldKey).Name];

        if (ExecuteCommand(_getCommand(eCommandString.Delete).Replace("#TABLENAME#", tdesc.Table.TableName).Replace("#FIELDNAMEKEY#", tdesc.FieldKey.Name).ToString(), keyvalue) == 1)
            ret = true;

        Database.AfterDelete?.Invoke(data);

        if (Silent) return ret;


        if (Transaction == null)
            MapperCore.EventHub.Deliver(data, eHubEventType.ObjectDeleted);
        else
        {
            lock (msgBufferLock)
            {
                _msgBuffer.Add(new Tuple<IDataObject, eHubEventType>(data, eHubEventType.ObjectDeleted));
            }
        }

        return ret;
    }

    /// <summary>
    /// delete a data object of type T with the given id
    /// </summary>
    /// <typeparam name="T">type of object to save</typeparam>
    /// <param name="id">id oft the object to delete</param>
    public bool Delete<T>(Guid id) where T : class, ITable, new()
    {
        T? data = Load<T>(id);

        if (data == null)
            throw new ArgumentException($@"There is no object with id {id} in database.", nameof(id));

        return Delete(data);
    }

    /// <summary>
    /// delete a list of data objects of type T
    /// </summary>
    /// <typeparam name="T">type of object to save</typeparam>
    /// <param name="data">list of objects to delete</param>
    public int Delete<T>(IEnumerable<T> data) where T : class, ITable
    {
        int ret = 0;

        foreach (T entry in data)
        {
            if (Delete(entry))
                ++ret;
        }

        return ret;
    }
    #endregion

    #region read/select
    /// <summary>
    /// select a single (the first tah equals to query) data object of type T
    /// </summary>
    /// <typeparam name="T">type of data object</typeparam>
    /// <param name="options">query options</param>
    /// <param name="query">query string</param>
    /// <param name="args">arguments for the query (parameters)</param>
    /// <returns>data object or default(T)</returns>
    public T? SelectSingle<T>(ReadOptions options, string query, params object[] args) where T : IDataObject, new()
    {
        T? ret = default;

        options.MaximumRecordCount = 1;

        using (TCommand cmd = _createSelect<T>(options, query, eQueryType.Select, args))
        {
            Database.TraceBeforeExecute?.Invoke(new TraceInfo(cmd.CommandText));
            using (DbDataReader? reader = GetReader(cmd))
            {
                if (reader == null)
                    throw new Exception(string.Format(Strings.ERR_GETREADER_NOT_AVAILABLE));

                int cols = reader.FieldCount;
                PropertyDescription[] dict = new PropertyDescription[cols];

                for (int i = 0; i < cols; i++)
                {
                    string fieldname = reader.GetName(i).ToLower();
                    PropertyDescription? prop = typeof(T).GetTypeDescription().Fields.Values.FirstOrDefault(f => f.Name.ToLower() == fieldname); 

                    if (prop != null)
                        dict[i] = prop;
                }

                if (reader.Read())
                    ret = ReadFromReader<T>(reader, dict);
            }
            Database.TraceAfterExecute?.Invoke(new TraceInfo(cmd.CommandText));
        }

        return ret;
    }




    /// <summary>
    /// select a single (the first tah equals to query) data object of type T
    /// </summary>
    /// <typeparam name="T">type of data object</typeparam>
    /// <param name="query">query string</param>
    /// <param name="args">arguments for the query (parameters)</param>
    /// <returns>data object or default(T)</returns>
    public T? SelectSingle<T>(string query, params object[] args) where T : IDataObject, new()
    {
        return SelectSingle<T>(new ReadOptions { MaximumRecordCount = 1 }, query, args);
    }

    /// <summary>
    /// load a single data object of type T by its primary key
    /// </summary>
    /// <typeparam name="T">type of data object</typeparam>
    /// <param name="guid">primary key</param>
    /// <returns>data object or default(T)</returns>
    public T? Load<T>(Guid guid) where T : IDataObject, new()
    {
        PropertyDescription? fldKey = typeof(T).GetTypeDescription().FieldKey;

        if (fldKey == null)
            throw new Exception($"Type {typeof(T).FullName} does not contain a key field.");

        string query = fldKey.Name + " = ?";

        return SelectSingle<T>(query, guid);
    }

    /// <summary>
    /// load a list of records from connection
    /// </summary>
    /// <typeparam name="T">Type to read</typeparam>
    /// <param name="options">query options</param>
    /// <returns></returns>
    public BindingList<T> Select<T>(ReadOptions options)
        where T : IDataObject, new()
    {
        return Select<T>(options, string.Empty, Array.Empty<object>());
    }

    /// <summary>
    /// load a list of records from connection
    /// </summary>
    /// <typeparam name="T">Type to read</typeparam>
    /// <param name="options">query options</param>
    /// <param name="query">query (where clause or complete select query)</param>
    /// <param name="args">arguments in query</param>
    /// <returns></returns>
    public BindingList<T> Select<T>(ReadOptions options, string query, params object[]? args) where T : IDataObject, new()
    {
        BindingList<T> ret = new();

        bool restore = ret.RaiseListChangedEvents;
        ret.RaiseListChangedEvents = false;

        using (TCommand cmd = _createSelect<T>(options, query, eQueryType.Select, args))
        {
            Database.TraceBeforeExecute?.Invoke(new TraceInfo(cmd.CommandText));

            using (DbDataReader? reader = GetReader(cmd))
            {
                if (reader == null)
                    throw new Exception(string.Format(Strings.ERR_GETREADER_NOT_AVAILABLE));

                int cols = reader.FieldCount;
                PropertyDescription[] dict = new PropertyDescription[cols];

                for (int i = 0; i < cols; i++)
                {
                    string fieldname = reader.GetName(i).ToLower();
                    PropertyDescription? prop = typeof(T).GetTypeDescription().Fields.Values
                        .FirstOrDefault(f => f.Name.ToLower() == fieldname);

                    if (prop != null)
                        dict[i] = prop;
                }

                while (reader.Read())
                {
                    if (options.Filter != null)
                    {
                        var record = ReadFromReader<T>(reader, dict);
                        
                        if (options.Filter(record))
                            ret.Add(record);
                    }
                    else
                        ret.Add(ReadFromReader<T>(reader, dict));
                }
            }

            Database.TraceAfterExecute?.Invoke(new TraceInfo(cmd.CommandText));
        }

        ret.RaiseListChangedEvents = restore;
        if (restore) ret.ResetBindings();

        return ret;
    }

    /// <summary>
    /// Create a forward reader for a query.
    ///
    /// This method must be implemented by the derived class!
    /// </summary>
    /// <param name="command">Query command to execute</param>
    /// <param name="recordCount">affected recors</param>
    /// <returns>the reader or null</returns>
    public virtual DbDataReader GetReader(TCommand command, out Int64 recordCount)
    {
        throw new NotImplementedException(Strings.ERROR_NOTIMPLEMENTEDINBASE);
    }

    /// <summary>
    /// Create a forward reader for a query.
    ///
    /// This method must be implemented by the derived class!
    /// </summary>
    /// <param name="command">Query command to execute</param>
    /// <returns>the reader or null</returns>
    public virtual DbDataReader GetReader(TCommand command)
    {
        throw new NotImplementedException(Strings.ERROR_NOTIMPLEMENTEDINBASE);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="query"></param>
    /// <param name="args"></param>
    /// <returns></returns>
    public BindingList<T> Select<T>(string query, params object[] args) where T : IDataObject, new()
    {
        return Select<T>(new ReadOptions(), query, args);
    }


    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public BindingList<T> Select<T>() where T : IDataObject, new()
    {
        return Select<T>(new ReadOptions(), "", null);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public IReader<T> SelectReader<T>() where T : IDataObject, new()
    {
        return SelectReader<T>(new ReadOptions(), "", null);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="query"></param>
    /// <param name="args"></param>
    /// <returns></returns>
    public IReader<T> SelectReader<T>(string query, params object[] args) where T : IDataObject, new()
    {
        return SelectReader<T>(new ReadOptions(), query, args);
    }


    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="options"></param>
    /// <param name="query"></param>
    /// <param name="args"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public IReader<T> SelectReader<T>(ReadOptions options, string query, params object[]? args) where T : IDataObject, new()
    {
        TCommand cmd = _createSelect<T>(options, query, eQueryType.Select, args);

        Database.TraceBeforeExecute?.Invoke(new TraceInfo(cmd.CommandText));

        var reader = GetReader(cmd);
        if (reader == null)
            throw new Exception(string.Format(Strings.ERR_GETREADER_NOT_AVAILABLE));

        DataReader<T> ret = new(reader, this);
        Database.TraceAfterExecute?.Invoke(new TraceInfo(cmd.CommandText));

        return ret;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public DataTable SelectDataTable<T>() where T : IDataObject, new()
    {
        return SelectDataTable<T>(new ReadOptions(), "", null);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="query"></param>
    /// <param name="args"></param>
    /// <returns></returns>
    public DataTable SelectDataTable<T>(string query, params object[] args) where T : IDataObject, new()
    {
        return SelectDataTable<T>(new ReadOptions(), query, args);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="options"></param>
    /// <param name="query"></param>
    /// <param name="args"></param>
    /// <returns></returns>
    public DataTable SelectDataTable<T>(ReadOptions options, string query, params object[]? args) where T : IDataObject, new()
    {
        DataTable ret = new();

        using (TCommand cmd = _createSelect<T>(options, query, eQueryType.Select, args))
        {
            Database.TraceBeforeExecute?.Invoke(new TraceInfo(cmd.CommandText));
            using (DbDataReader? reader = GetReader(cmd))
            {
                if (reader == null)
                    throw new Exception(string.Format(Strings.ERR_GETREADER_NOT_AVAILABLE));

                ret.Load(reader);
            }
            Database.TraceAfterExecute?.Invoke(new TraceInfo(cmd.CommandText));
        }

        return ret;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="query"></param>
    /// <param name="args"></param>
    /// <returns></returns>
    public DataTable SelectDataTable(string query, params object[] args)
    {
        DataTable ret = new();

        using (TCommand cmd = _parseCommand(query, args))
        {
            Database.TraceBeforeExecute?.Invoke(new TraceInfo(cmd.CommandText));
            using (DbDataReader? reader = GetReader(cmd))
            {
                if (reader == null)
                    throw new Exception(string.Format(Strings.ERR_GETREADER_NOT_AVAILABLE));

                ret.Load(reader);
            }
            Database.TraceAfterExecute?.Invoke(new TraceInfo(cmd.CommandText));
        }

        return ret;
    }

    /// <summary>
    /// Loads data for the property with a specific name from a record with the given id
    /// </summary>
    /// <typeparam name="T">Type of the column/property</typeparam>
    /// <param name="type">Type which defines the table or view</param>
    /// <param name="id">ID of the record</param>
    /// <param name="name">name of column</param>
    /// <returns>value for the field</returns>
    public T? LoadValue<T>(Type type, Guid id, string name)
    {
        TypeDescription tdesc = type.GetTypeDescription();

        PropertyDescription? fldKey = tdesc.FieldKey;

        if (fldKey == null)
            throw new Exception(string.Format(Strings.ERR_TYPE_DOES_NOT_CONTAIN_KEY, typeof(T).FullName));

        string query = new StringBuilder(Database.Translator.GetCommandString(eCommandString.LoadValue))
            .Replace("#FIELDNAME#", tdesc.Fields[name].Name)
            .Replace("#TABLENAME#", tdesc.Table != null ? tdesc.Table?.TableName : tdesc.View?.ViewName)
            .Replace("#FIELDNAMEKEY#", fldKey.Name).ToString();

        return ExecuteCommandScalar<T>(query, id);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="id"></param>
    /// <returns></returns>
    public bool Exist<T>(Guid id) where T : ITable
    {
        bool ret = false;

        TypeDescription tdesc = typeof(T).GetTypeDescription();

        PropertyDescription? fldKey = tdesc.FieldKey;

        if (fldKey == null)
            throw new(string.Format(Strings.ERR_TYPE_DOES_NOT_CONTAIN_KEY, typeof(T).FullName));

        string field = fldKey.Name;
        string table = tdesc.Table != null ? tdesc.Table.TableName : tdesc.View != null ? tdesc.View.ViewName : throw new(typeof(T).FullName + " is not a table or view. This method is only for tables or views.");
        string qry = _getCommand(eCommandString.Exist).Replace("#TABLENAME#", table).Replace("#FIELDNAMEKEY#", field).ToString();

        using (TCommand cmd = _parseCommand(qry, new object[] { id }))
        {
            cmd.CommandType = CommandType.Text;
            Database.TraceBeforeExecute?.Invoke(new TraceInfo(cmd.CommandText));
            var found = cmd.ExecuteScalar();

            if (found == null) return ret;

            var guid = Database.Translator.FromDatabase(found, typeof(Guid));
                
            if (guid is Guid g)
            {
                Database.TraceAfterExecute?.Invoke(new TraceInfo(cmd.CommandText));

                ret = g.Equals(id);
            }
        }

        return ret;
    }

    /// <summary>
    /// count rows in database
    /// </summary>
    /// <typeparam name="T">object type (table/view)</typeparam>
    /// <returns>row count</returns>
    public int Count<T>() where T : IDataObject
    {
        return Count<T>(new ReadOptions(), "", null);
    }

    /// <summary>
    /// count rows in database
    /// </summary>
    /// <typeparam name="T">object type (table/view)</typeparam>
    /// <param name="query">sql query to filter specific rows</param>
    /// <param name="args">arguments for the query</param>
    /// <returns>row count</returns>
    public int Count<T>(string query, params object[] args) where T : IDataObject
    {
        return Count<T>(new ReadOptions(), query, args);
    }

    /// <summary>
    /// count rows in database
    /// </summary>
    /// <typeparam name="T">object type (table/view)</typeparam>
    /// <param name="options">query options</param>
    /// <param name="query">sql query to filter specific rows</param>
    /// <param name="args">arguments for the query</param>
    /// <returns>row count</returns>
    public int Count<T>(ReadOptions options, string query, params object[]? args) where T : IDataObject
    {
        int ret;

        PropertyDescription? fldKey = typeof(T).GetTypeDescription().FieldKey;

        if (fldKey == null)
            throw new Exception(string.Format(Strings.ERR_TYPE_DOES_NOT_CONTAIN_KEY, typeof(T).FullName));

        options.Fields = new [] { fldKey.Name };

        using (TCommand cmd = _createSelect<T>(options, query, eQueryType.Count, args))
        {
            Database.TraceBeforeExecute?.Invoke(new TraceInfo(cmd.CommandText));
            ret = Convert.ToInt32(cmd.ExecuteScalar());
            Database.TraceAfterExecute?.Invoke(new TraceInfo(cmd.CommandText));
        }

        return ret;
    }

    /// <summary>
    /// select summary for a sepecific column in database which holds numeric values
    /// </summary>
    /// <typeparam name="T">object type (table/view)</typeparam>
    /// <param name="field">field to summarize</param>
    /// <returns>sum of column values in all selected rows</returns>
    public decimal Sum<T>(string field) where T : IDataObject
    {
        return Sum<T>(new ReadOptions(), field, "", null);
    }

    /// <summary>
    /// select summary for a sepecific column in database which holds numeric values
    /// </summary>
    /// <typeparam name="T">object type (table/view)</typeparam>
    /// <param name="field">field to summarize</param>
    /// <param name="query">sql query to filter specific rows</param>
    /// <param name="args">arguments for the query</param>
    /// <returns>sum of column values in all selected rows</returns>
    public decimal Sum<T>(string field, string query, params object[] args) where T : IDataObject
    {
        return Sum<T>(new ReadOptions(), field, query, args);
    }

    /// <summary>
    /// select summary for a sepecific column in database which holds numeric values
    /// </summary>
    /// <typeparam name="T">object type (table/view)</typeparam>
    /// <param name="options">query options</param>
    /// <param name="field">field to summarize</param>
    /// <param name="query">sql query to filter specific rows</param>
    /// <param name="args">arguments for the query</param>
    /// <returns>sum of column values in all selected rows</returns>
    public decimal Sum<T>(ReadOptions options, string field, string query, params object[]? args) where T : IDataObject
    {
        decimal ret;

        options.Fields = new[] { field };

        using (TCommand cmd = _createSelect<T>(options, query, eQueryType.Sum, args))
        {
            Database.TraceBeforeExecute?.Invoke(new TraceInfo(cmd.CommandText));
            ret = Convert.ToDecimal(cmd.ExecuteScalar());
            Database.TraceAfterExecute?.Invoke(new TraceInfo(cmd.CommandText));
        }

        return ret;
    }

    /// <summary>
    /// read a single row/object from a reader
    /// </summary>
    /// <typeparam name="T">type of object</typeparam>
    /// <param name="reader">reader</param>
    /// <param name="dict">dictionary with all properties to read</param>
    /// <returns>row object or default(T)</returns>
    public T ReadFromReader<T>(DbDataReader reader, PropertyDescription[] dict) where T : IDataObject, new()
    {
        T ret = new()
        {
            Database = Database
        };

        TypeDescription tdesc = typeof(T).GetTypeDescription();

        int fields = reader.FieldCount;
        for (int i = 0; i < fields; i++)
        {
            PropertyInfo pinfo = (PropertyInfo)dict[i];

            if (pinfo.CanWrite == false)
                continue;

            if (pinfo.PropertyType == typeof(short))
                tdesc.Accessor[ret, pinfo.Name] = Convert.ToInt16(Database.Translator.FromDatabase(reader.GetValue(i), pinfo.PropertyType));
            else if (pinfo.PropertyType == typeof(byte))
                tdesc.Accessor[ret, pinfo.Name] = Convert.ToByte(Database.Translator.FromDatabase(reader.GetValue(i), pinfo.PropertyType));
            else
                tdesc.Accessor[ret, pinfo.Name] = Database.Translator.FromDatabase(reader.GetValue(i), pinfo.PropertyType);
        }

        ret.CommitChanges();
        ret.AfterLoad();

        return ret;

    }
    #endregion

    #region private methods
    private void _checkView(TypeDescription tdesc)
    {
        if (tdesc.View == null)
            throw new Exception($"Type {tdesc.Type.FullName} is not a view.");

        string viewname = tdesc.View.ViewName;

        if (ExistView(viewname))
            DropView(viewname);

        string fields = "";
        string fieldsonly = "";
        string sourcefields = "";

        foreach (PropertyDescription field in tdesc.Fields.Values)
        {
            if (field.Field == null)
                continue;

            string fieldname = field.Name;

            if (field.Field.SourceField.IsEmpty())
                fieldsonly += fieldname + ", ";
            else
                fields += fieldname + ", ";

            if (field.Name.IsEmpty())
                continue;

            if (field.Field.SourceField.IsEmpty())
                continue;

            sourcefields += field.Field.SourceField + " as " + fieldname + ", ";
        }

        fields = fields + fieldsonly;

        if (fields.EndsWith(", "))
            fields = fields.Substring(0, fields.Length - 2);

        if (sourcefields.EndsWith(", "))
            sourcefields = sourcefields.Substring(0, sourcefields.Length - 2);

        CreateView(viewname, fields, tdesc.View.Query.Replace("#FIELDS#", sourcefields));
    }

    private void _checkTable(TypeDescription tdesc)
    {
        if (tdesc.Table == null)
            throw new Exception($"Type {tdesc.Type.FullName} is not a table.");

        if (tdesc.FieldKey == null)
            throw new Exception($"Type {tdesc.Type.FullName} does not contain a 'key' field.");

        if (tdesc.FieldCreated == null)
            throw new Exception($"Type {tdesc.Type.FullName} does not contain a 'created' field.");

        if (tdesc.FieldChanged == null)
            throw new Exception($"Type {tdesc.Type.FullName} does not contain a 'changed' field.");


        string tablename = Database.GetName(tdesc.Table.TableName);

        if (ExistTable(tablename) == false)
        {
            BeginTransaction();
            try
            {
                CreateTable(
                    tablename,
                    tdesc.FieldKey.Name,
                    tdesc.FieldCreated.Name,
                    tdesc.FieldChanged.Name
                );
                CommitTransaction();
            }
            catch (Exception ex)
            {
                RollbackTransaction();
                throw new Exception($"Error creating table {tablename}.", ex);
            }
        }

        DataTable? scheme = GetScheme(tablename);
        List<DataRow> processedRows = new();

        foreach (PropertyDescription prop in tdesc.Fields.Values)
        {
            if (prop.Field == null)
                continue;

            DataRow? row = scheme?.AsEnumerable()
                .FirstOrDefault(r => r.Field<string>("ColumnName")?.ToLower() == prop.Name.ToLower());

            if (row == null)
            {
                StringBuilder query = _getCommand(eCommandString.CreateField).Replace("#FIELDNAME#", prop.Name)
                    .Replace("#NAME#", prop.Name);
                if (((PropertyInfo)prop).PropertyType.IsEnum)
                    query.Replace("#FIELDOPTIONS#", _getCommand(eCommandString.FieldDefInt32).ToString());
                else if (((PropertyInfo)prop).PropertyType == typeof(bool))
                    query.Replace("#FIELDOPTIONS#", _getCommand(eCommandString.FieldDefBool).ToString());
                else if (((PropertyInfo)prop).PropertyType == typeof(byte))
                    query.Replace("#FIELDOPTIONS#", _getCommand(eCommandString.FieldDefByte).ToString());
                else if (((PropertyInfo)prop).PropertyType == typeof(DateTime))
                    query.Replace("#FIELDOPTIONS#", _getCommand(eCommandString.FieldDefDateTime).ToString());
                else if (((PropertyInfo)prop).PropertyType == typeof(decimal))
                    query.Replace("#FIELDOPTIONS#", _getCommand(eCommandString.FieldDefDecimal).ToString());
                else if (((PropertyInfo)prop).PropertyType == typeof(double))
                    query.Replace("#FIELDOPTIONS#", _getCommand(eCommandString.FieldDefDouble).ToString());
                else if (((PropertyInfo)prop).PropertyType == typeof(float))
                    query.Replace("#FIELDOPTIONS#", _getCommand(eCommandString.FieldDefFloat).ToString());
                else if (((PropertyInfo)prop).PropertyType == typeof(Guid))
                    query.Replace("#FIELDOPTIONS#", _getCommand(eCommandString.FieldDefGuid).ToString());
                else if (((PropertyInfo)prop).PropertyType == typeof(Image))
                    query.Replace("#FIELDOPTIONS#", _getCommand(eCommandString.FieldDefImage).ToString());
                else if (((PropertyInfo)prop).PropertyType == typeof(int))
                    query.Replace("#FIELDOPTIONS#", _getCommand(eCommandString.FieldDefInt32).ToString());
                else if (((PropertyInfo)prop).PropertyType == typeof(short))
                    query.Replace("#FIELDOPTIONS#", _getCommand(eCommandString.FieldDefInt16).ToString());
                else if (((PropertyInfo)prop).PropertyType == typeof(long))
                    query.Replace("#FIELDOPTIONS#", _getCommand(eCommandString.FieldDefInt64).ToString());
                else if (((PropertyInfo)prop).PropertyType == typeof(Type))
                    query.Replace("#FIELDOPTIONS#", _getCommand(eCommandString.FieldDefType).ToString());
                else if (((PropertyInfo)prop).PropertyType == typeof(byte[]))
                    query.Replace("#FIELDOPTIONS#", _getCommand(eCommandString.FieldDefBinary).ToString());
                else if (((PropertyInfo)prop).PropertyType == typeof(string))
                {
                    query.Replace("#FIELDOPTIONS#",
                        prop.Field.MaxLength == -1
                            ? _getCommand(eCommandString.FieldDefMemo).ToString()
                            : _getCommand(eCommandString.FieldDefString).ToString());
                }
                else
                {
                    if (((PropertyInfo)prop).PropertyType.IsSerializable)
                        query.Replace("#FIELDOPTIONS#", _getCommand(eCommandString.FieldDefObject).ToString());
                    else
                    {
                        throw new Exception(
                            $"Unknown data type {((PropertyInfo)prop).PropertyType.FullName} can not be used as field.");
                    }
                }

                query = query
                    .Replace("#TABLENAME#", tablename)
                    .Replace("#SIZE#", prop.Field.MaxLength.ToString().Trim())
                    .Replace("#BLOCKSIZE#", prop.Field.BlobBlockSize.ToString().Trim());

                ExecuteCommand(query.ToString(), null);
            }
            else
            {
                processedRows.Add(row);
                if (((PropertyInfo)prop).PropertyType == typeof(string) &&
                    (int)row["ColumnSize"] < prop.Field.MaxLength)
                {
                    StringBuilder query = _getCommand(eCommandString.AlterFieldLength)
                        .Replace("#FIELDNAME#", prop.Name)
                        .Replace("#TABLENAME#", tablename)
                        .Replace("#SIZE#", prop.Field.MaxLength.ToString().Trim());

                    ExecuteCommand(query.ToString(), null);
                }
            }

            if (prop.Field.Indexed)
            {
                string idxName = "IDX_" + prop.Name;
                string idxExpr;

                if (prop.Field.SystemFieldFlag != eSystemFieldFlag.None)
                    idxName += "_" + tdesc.Table?.TableId.ToString().Trim();

                if (prop.Field.IndexDefinition != null && !prop.Field.IndexDefinition.IsEmpty())
                    idxExpr = prop.Field.IndexDefinition;
                else
                    idxExpr = prop.Name;

                if (ExistIndex(idxName, tablename))
                    DropIndex(idxName, tablename);

                CreateIndex(idxName, tablename, idxExpr, prop.Field.Unique, false);
            }

            if (prop.Field.ConstraintType == null || prop.Field.ConstraintType == tdesc.Type) continue;

            TypeDescription tdescRef = prop.Field.ConstraintType.GetTypeDescription();

            if (tdescRef == null || tdescRef.Table == null)
            {
                throw new InvalidOperationException(
                    $"Reference Type {prop.Field.ConstraintType.FullName} has no type description or is not a table.");
            }

            if (tdescRef.FieldKey == null)
            {
                throw new InvalidOperationException(
                    $"Reference Type {prop.Field.ConstraintType.FullName} has no primary key field (SYS_ID or equal).");
            }


            string fkeyName = "FKEY_" + prop.Name;

            if (ExistConstraint(fkeyName, tablename)) continue;

            // prüfen ob die Zieltabelle existiert und wenn nicht vorher anlegen...
            if (!ExistTable(tdescRef.Table.TableName))
                Check(prop.Field.ConstraintType);

            CreateForeignKeyConstraint(fkeyName, tablename, prop.Name, tdescRef.Table.TableName,
                tdescRef.FieldKey.Name, prop.Field.ConstraintUpdate, prop.Field.ConstraintDelete);
        }

        if (!Database.Configuration.AllowDropColumns || scheme == null) return;

        foreach (DataRow row in scheme.Rows)
        {
            if (processedRows.Contains(row))
                continue;

            // remove Index 
            string idxName = "IDX_" + row.Field<string>("ColumnName");

            if (ExistIndex(idxName, tablename))
                DropIndex(idxName, tablename);

            // Drop row...
            string query = _getCommand(eCommandString.DropField)
                .Replace("#FIELDNAME#", row.Field<string>("ColumnName"))
                .Replace("#TABLENAME#", tablename).ToString();

            ExecuteCommand(query, null);
        }
    }

    private StringBuilder _getCommand(eCommandString command) { return new StringBuilder(Database.Translator.GetCommandString(command)); }

    private string _translateEventType(eTriggerEvent code) { return Database.Translator.GetTriggerEvent(code); }

    /// <summary>
    /// Erzeugt aus einem SQL-Query und optionalen Parametern eine parametrisiertes FbCommand-Objekt, dass dann ausgeführt werden kann.
    /// </summary>
    /// <param name="command">SQL-Query</param>
    /// <param name="args">optionale Parameter</param>
    /// <returns>DBCommand für die Ausführung</returns>
    internal TCommand _parseCommand(string command, object[]? args)
    {
        TCommand cmd = new()
        {
            Connection = Connection,
            Transaction = Transaction
        };

        int cnt = 0;

        if (args != null && args.Length > 0)
        {
            foreach (object o in args[0] is IEnumerable<object> ? (IEnumerable<object>)args[0] : args)
            {
                cmd.Parameters.Add(new TParameter { ParameterName = "p" + cnt, Value = Database.Translator.ToDatabase(o) });
                ++cnt;
            }

            cnt = 0;

            StringBuilder replacer = new();

            foreach (char c in command)
            {
                if (c == '?')
                {
                    replacer.Append("@p");
                    replacer.Append(cnt);
                    ++cnt;
                }
                else
                    replacer.Append(c);
            }

            command = replacer.ToString();
        }

        cmd.CommandText = Database.Translator.TranslateQuery(ref command);

        return cmd;
    }

    internal TCommand _createSelect<T>(ReadOptions options, string query, eQueryType queryType, params object[]? args)
    {
        string qry = "";
        StringBuilder sbQry = new();

        TypeDescription tdesc = typeof(T).GetTypeDescription();

        if (query.IsEmpty() == false && (query.Contains("&&") || query.Contains("||")))
            query = query.Replace("&&", "and").Replace("||", "or");

        if (query.ToUpper().StartsWith("SELECT"))
            sbQry.Append(query);
        else
        {
            if (queryType == eQueryType.Count)
                sbQry.Append(_getCommand(eCommandString.SelectCount).Replace("#FIELDNAME#", options.Fields[0]));
            else if (queryType == eQueryType.Sum)
                sbQry.Append(_getCommand(eCommandString.SelectSum).Replace("#FIELDNAME#", options.Fields[0]));
            else if (queryType == eQueryType.Select)
                sbQry.Append(options.MaximumRecordCount > 0 ? _getCommand(eCommandString.SelectTop) : _getCommand(eCommandString.Select));

            if (query.IsNotEmpty())
            {
                sbQry.Append(" WHERE ");
                sbQry.Append(query);
            }
            if (options.OrderBy.IsNotEmpty())
            {
                sbQry.Append(" ORDER BY ");
                sbQry.Append(options.OrderBy);

                if (options.OrderMode == eOrderMode.Descending)
                    sbQry.Append(" DESC");
            }

            if (options.GroupOn.IsNotEmpty())
            {
                sbQry.Append(" GROUP ON ");
                sbQry.Append(options.GroupOn);
            }

            if (options.MaximumRecordCount > 0)
            {
                if (Database.Configuration.DatabaseType == eDatabaseType.PostgreSql)
                    sbQry.Append(" LIMIT #COUNT# ");

                sbQry.Replace("#COUNT#", options.MaximumRecordCount.ToString().Trim());
            }
        
            qry = sbQry.ToString();

            if (qry.Contains("#FIELDNAMES#"))
            {
                StringBuilder sbFields = new();

                if (options.Fields.Length < 1 && (options.IgnoreDelayed || tdesc.Fields.Values.FirstOrDefault(dsc => dsc.Field != null && dsc.Field.Delayed) == null))
                    sbFields.Append("*");
                else
                {
                    foreach (PropertyDescription desc in tdesc.Fields.Values)
                    {
                        if (desc.Field == null)
                            continue;

                        if (options.Fields.Length > 0 && Array.Find(options.Fields, fld => Database.GetName(fld) ==  Database.GetName(((PropertyInfo)desc).Name)) == null && desc.Field.SystemFieldFlag != eSystemFieldFlag.PrimaryKey)
                            continue;

                        if (desc.Field.Delayed && options.IgnoreDelayed == false)
                            continue;

                        if (sbFields.Length > 0)
                            sbFields.Append(", ");

                        sbFields.Append(Database.GetName(((PropertyInfo)desc).Name));
                    }
                }

                qry = qry.Replace("#FIELDNAMES#", sbFields.ToString());
            }
        }

        if (qry.Contains("#TABLENAME#"))
            qry = qry.Replace("#TABLENAME#", tdesc.IsTable ? tdesc.Table?.TableName : tdesc.View?.ViewName);

        return _parseCommand(qry, args);
    }

    #endregion

    #region structure
    /// <summary>
    /// select scheme of a table/view from database as datatable with informations about any column in table/view
    /// </summary>
    /// <param name="tableviewName">name of the table/view in database</param>
    /// <returns>scheme as datatable or null if no scheme is available</returns>
    public DataTable? GetScheme(string tableviewName)
    {
        DataTable? scheme;
        using (TCommand cmd = new())
        {
            cmd.CommandText = _getCommand(eCommandString.GetSchema).Replace("#NAME#", tableviewName).ToString();
            cmd.CommandType = CommandType.Text;
            cmd.Connection = Connection;
            cmd.Transaction = Transaction;

            Database.TraceBeforeExecute?.Invoke(new TraceInfo(cmd.CommandText));
            using (var reader = cmd.ExecuteReader(CommandBehavior.SchemaOnly)) scheme = reader.GetSchemaTable();
            Database.TraceBeforeExecute?.Invoke(new TraceInfo(cmd.CommandText));
        }

        return scheme;
    }

    /// <summary>
    /// Checks if table/view for a specific Type which represents a persistent type exists
    /// </summary>
    /// <typeparam name="T">persistent type (table or view)</typeparam>
    public void Check<T>() where T : IDataObject
    {
        Check<T>(false);
    }

    /// <summary>
    /// Checks if table/view for a specific Type which represents a persistent type exists
    /// </summary>
    /// <param name="type">persistent type (table or view)</param>
    public void Check(Type type)
    {
        Check(type, false);
    }

    /// <summary>
    /// Checks if table/view for a specific Type which represents a persistent type exists
    /// </summary>
    /// <typeparam name="T">persistent type (table or view)</typeparam>
    /// <param name="force">force complete check</param>
    public void Check<T>(bool force) where T : IDataObject
    {
        Check(typeof(T), force);
    }

    /// <summary>
    /// Checks if table/view for a specific Type which represents a persistent type exists
    /// </summary>
    /// <param name="type">persistent type (table or view)</param>
    /// <param name="force">force complete check</param>
    public void Check(Type type, bool force)
    {
        TypeDescription tdesc = type.GetTypeDescription();
        SystemDatabaseInformation? dbinfo = null;

        if (type == typeof(SystemDatabaseInformation))
        {
            _checkTable(tdesc);
            return;
        }

        bool updateVersion = false;

        if (tdesc.IsTable && tdesc.Table != null)
        {
            dbinfo = SelectSingle<SystemDatabaseInformation>($"{nameof(SystemDatabaseInformation.SYSINFO_TABLENAME)} = ?", tdesc.Table.TableName) ??
                     new SystemDatabaseInformation
            {
                SYSINFO_DBVERSION = 0,
                SYSINFO_TABLENAME = tdesc.Table.TableName,
                SYSINFO_IDENTIFIER = tdesc.Table.TableId
            };

            if (force || dbinfo.SYSINFO_DBVERSION < tdesc.Table.Version)
            {
                _checkTable(tdesc);

                if (dbinfo.SYSINFO_DBVERSION != tdesc.Table.Version)
                {
                    dbinfo.SYSINFO_DBVERSION = tdesc.Table.Version;
                    updateVersion = true;
                }
            }
        }
        else if (tdesc.IsView && tdesc.View != null)
        {
            dbinfo = SelectSingle<SystemDatabaseInformation>($"{nameof(SystemDatabaseInformation.SYSINFO_TABLENAME)} = ?", tdesc.View.ViewName) ??
                     new SystemDatabaseInformation
            {
                SYSINFO_DBVERSION = 0,
                SYSINFO_TABLENAME = tdesc.View.ViewName,
                SYSINFO_IDENTIFIER = tdesc.View.ViewId
            };

            if (force || dbinfo.SYSINFO_DBVERSION < tdesc.View.Version)
            {
                _checkView(tdesc);


                if (dbinfo.SYSINFO_DBVERSION != tdesc.View.Version)
                {
                    dbinfo.SYSINFO_DBVERSION = tdesc.View.Version;
                    updateVersion = true;
                }
            }
        }

        if (dbinfo != null && updateVersion)
            Save(dbinfo);
    }

    /// <summary>
    /// checks if a table exists
    /// </summary>
    /// <param name="tableName">table name</param>
    /// <returns>true table exists</returns>
    public bool ExistTable(string tableName)
    {
        return ExecuteCommandScalar<int>(_getCommand(eCommandString.ExistTable).Replace("#NAME#", Database.GetName(tableName)).ToString()) > 0;
    }

    /// <summary>
    /// checks if a procedure exists
    /// </summary>
    /// <param name="procedureName">procedure name</param>
    /// <returns>true procedure exists</returns>
    public virtual bool ExistProcedure(string procedureName)
    {
        return ExecuteCommandScalar<int>(_getCommand(eCommandString.ExistProcedure).Replace("#NAME#", Database.GetName(procedureName)).ToString()) > 0;
    }

    /// <summary>
    /// checks if a trigger exists
    /// </summary>
    /// <param name="triggerName">trigger name</param>
    /// <returns>true trigger exists</returns>
    public bool ExistTrigger(string triggerName)
    {
        return ExecuteCommandScalar<int>(_getCommand(eCommandString.ExistTrigger).Replace("#NAME#", Database.GetName(triggerName)).ToString()) > 0;
    }

    /// <summary>
    /// checks if a view exists
    /// </summary>
    /// <param name="viewName">view name</param>
    /// <returns>true view exists</returns>
    public bool ExistView(string viewName)
    {
        return ExecuteCommandScalar<int>(_getCommand(eCommandString.ExistView).Replace("#NAME#", Database.GetName(viewName)).ToString()) > 0;
    }

    /// <summary>
    /// checks if a index exists
    /// </summary>
    /// <param name="indexName">index name</param>
    /// <param name="tablename">name of table</param>
    /// <returns>true if index exists</returns>
    public bool ExistIndex(string indexName, string tablename)
    {
        return ExecuteCommandScalar<int>(_getCommand(eCommandString.ExistIndex).Replace("#NAME#", Database.GetName(indexName)).Replace("#TABLENAME#", tablename).ToString()) > 0;
    }

    /// <summary>
    /// checks if a foreign key constraint exist
    /// </summary>
    /// <param name="constraintName">index name</param>
    /// <param name="tablename">name of table</param>
    /// <returns>true if index exists</returns>
    public bool ExistConstraint(string constraintName, string tablename)
    {
        return ExecuteCommandScalar<int>(_getCommand(eCommandString.ExistConstraint).Replace("#NAME#", Database.GetName(constraintName)).Replace("#TABLENAME#", tablename).ToString()) > 0;
    }

    /// <summary>
    /// delete a table from database
    /// </summary>
    /// <param name="tableName">table name</param>
    public void DropTable(string tableName)
    {
        ExecuteCommand(_getCommand(eCommandString.BeforeAlterSchema).Replace("#DBNAME#", Database.Configuration.DatabaseName).ToString());
        ExecuteCommand(_getCommand(eCommandString.DropTable).Replace("#NAME#", Database.GetName(tableName)).ToString());
        ExecuteCommand(_getCommand(eCommandString.AfterAlterSchema).Replace("#DBNAME#", Database.Configuration.DatabaseName).ToString());
    }

    /// <summary>
    /// delete a procedure from database
    /// </summary>
    /// <param name="procedureName">procedure name</param>
    public virtual void DropProcedure(string procedureName)
    {
        ExecuteCommand(_getCommand(eCommandString.BeforeAlterSchema).Replace("#DBNAME#", Database.Configuration.DatabaseName).ToString());
        ExecuteCommand(_getCommand(eCommandString.DropProcedure).Replace("#NAME#", Database.GetName(procedureName)).ToString());
        ExecuteCommand(_getCommand(eCommandString.AfterAlterSchema).Replace("#DBNAME#", Database.Configuration.DatabaseName).ToString());
    }

    /// <summary>
    /// delete a trigger from database
    /// </summary>
    /// <param name="triggerName">trigger name</param>
    public void DropTrigger(string triggerName)
    {
        ExecuteCommand(_getCommand(eCommandString.BeforeAlterSchema).Replace("#DBNAME#", Database.Configuration.DatabaseName).ToString());
        ExecuteCommand(_getCommand(eCommandString.DropTrigger).Replace("#NAME#", Database.GetName(triggerName)).ToString());
        ExecuteCommand(_getCommand(eCommandString.AfterAlterSchema).Replace("#DBNAME#", Database.Configuration.DatabaseName).ToString());
    }

    /// <summary>
    /// delete a view from database 
    /// </summary>
    /// <param name="viewName">view name</param>
    public void DropView(string viewName)
    {
        ExecuteCommand(_getCommand(eCommandString.BeforeAlterSchema).Replace("#DBNAME#", Database.Configuration.DatabaseName).ToString());
        ExecuteCommand(_getCommand(eCommandString.DropView).Replace("#NAME#", Database.GetName(viewName)).ToString());
        ExecuteCommand(_getCommand(eCommandString.AfterAlterSchema).Replace("#DBNAME#", Database.Configuration.DatabaseName).ToString());
    }

    /// <summary>
    /// delete a index from database
    /// </summary>
    /// <param name="indexName">index name</param>
    /// <param name="tableName">table name</param>
    public void DropIndex(string indexName, string tableName)
    {
        ExecuteCommand(_getCommand(eCommandString.BeforeAlterSchema).Replace("#DBNAME#", Database.Configuration.DatabaseName).ToString());
        ExecuteCommand(_getCommand(eCommandString.DropIndex)
            .Replace("#NAME#", Database.GetName(indexName))
            .Replace("#TABLENAME#", Database.GetName(tableName))
            .Replace("#DBNAME#", Database.Configuration.DatabaseName).ToString());
        ExecuteCommand(_getCommand(eCommandString.AfterAlterSchema).Replace("#DBNAME#", Database.Configuration.DatabaseName).ToString());
    }


    /// <summary>
    /// delete a foreign key constraint from database
    /// </summary>
    /// <param name="constraintName">constraint name</param>
    /// <param name="tableName">table name</param>
    public void DropConstraint(string constraintName, string tableName)
    {
        ExecuteCommand(_getCommand(eCommandString.BeforeAlterSchema).Replace("#DBNAME#", Database.Configuration.DatabaseName).ToString());
        ExecuteCommand(_getCommand(eCommandString.DropConstraint)
            .Replace("#NAME#", Database.GetName(constraintName))
            .Replace("#TABLENAME#", Database.GetName(tableName))
            .Replace("#DBNAME#", Database.Configuration.DatabaseName).ToString());
        ExecuteCommand(_getCommand(eCommandString.AfterAlterSchema).Replace("#DBNAME#", Database.Configuration.DatabaseName).ToString());
    }



    /// <summary>
    /// Create a table
    /// </summary>
    /// <param name="tableName">table name</param>
    /// <param name="keyField">primary key field name</param>
    /// <param name="createdField">field name for created at timestamp</param>
    /// <param name="changedField">field name for changed at timestamp</param>
    public void CreateTable(string tableName, string keyField, string createdField, string changedField)
    {
        ExecuteCommand(_getCommand(eCommandString.CreateTable)
            .Replace("#NAME#", Database.GetName(tableName))
            .Replace("#TABLENAME#", Database.GetName(tableName))
            .Replace("#FIELDNAMEKEY#", Database.GetName(keyField))
            .Replace("#FIELDNAMECREATED#", Database.GetName(createdField))
            .Replace("#FIELDNAMECHANGED#", Database.GetName(changedField)).ToString());

        StringBuilder cmd = _getCommand(eCommandString.CreateKeyField);

        if (cmd.ToString().IsNotEmpty())
        {
            ExecuteCommand(cmd
                .Replace("#NAME#", Database.GetName(tableName))
                .Replace("#TABLENAME#", Database.GetName(tableName))
                .Replace("#FIELDNAMEKEY#", Database.GetName(keyField))
                .Replace("#FIELDNAMECREATED#", Database.GetName(createdField))
                .Replace("#FIELDNAMECHANGED#", Database.GetName(changedField)).ToString());
        }

        ExecuteCommand(_getCommand(eCommandString.TriggerBeforeInsert)
            .Replace("#NAME#", Database.GetName(tableName))
            .Replace("#TABLENAME#", Database.GetName(tableName))
            .Replace("#FIELDNAMEKEY#", Database.GetName(keyField))
            .Replace("#FIELDNAMECREATED#", Database.GetName(createdField))
            .Replace("#FIELDNAMECHANGED#", Database.GetName(changedField)).ToString());

        ExecuteCommand(_getCommand(eCommandString.TriggerBeforeUpdate)
            .Replace("#NAME#", Database.GetName(tableName))
            .Replace("#TABLENAME#", Database.GetName(tableName))
            .Replace("#FIELDNAMEKEY#", Database.GetName(keyField))
            .Replace("#FIELDNAMECREATED#", Database.GetName(createdField))
            .Replace("#FIELDNAMECHANGED#", Database.GetName(changedField)).ToString());
    }


    /// <summary>
    /// Create a procedure
    /// </summary>
    /// <param name="procedureName">procedure name</param>
    /// <param name="code">procedure code</param>
    public virtual void CreateProcedure(string procedureName, string code)
    {

    }

    /// <summary>
    /// Create a trigger
    /// </summary>
    /// <param name="triggerName">trigger name</param>
    /// <param name="tableName">table name</param>
    /// <param name="eventType">trigger event</param>
    /// <param name="code">trigger source code</param>
    public void CreateTrigger(string triggerName, string tableName, eTriggerEvent eventType, string code)
    {
        string eventCode = "";
        switch (eventType)
        {
            case eTriggerEvent.AfterDelete:
                eventCode = _getCommand(eCommandString.EventAfterDelete).ToString();
                break;
            case eTriggerEvent.BeforeInsert:
                eventCode = _getCommand(eCommandString.EventBeforeInsert).ToString();
                break;
            case eTriggerEvent.BeforeUpdate:
                eventCode = _getCommand(eCommandString.EventBeforeUpdate).ToString();
                break;
            case eTriggerEvent.BeforeDelete:
                eventCode = _getCommand(eCommandString.EventBeforeDelete).ToString();
                break;
            case eTriggerEvent.AfterInsert:
                eventCode = _getCommand(eCommandString.EventAfterInsert).ToString();
                break;
            case eTriggerEvent.AfterUpdate:
                eventCode = _getCommand(eCommandString.EventAfterUpdate).ToString();
                break;
        }

        ExecuteCommand(_getCommand(eCommandString.CreateTrigger)
            .Replace("#NAME#", Database.GetName(triggerName))
            .Replace("#TABLENAME#", Database.GetName(tableName))
            .Replace("#EVENT#", eventType.ToString())
            .Replace("#EVENTCODE#", eventCode)
            .Replace("#CODE#", code).ToString());
    }

    /// <summary>
    /// Create as view
    /// </summary>
    /// <param name="viewName">view name</param>
    /// <param name="fields">view fields</param>
    /// <param name="query">view query</param>
    public void CreateView(string viewName, string fields, string query)
    {
        ExecuteCommand(_getCommand(eCommandString.CreateView)
           .Replace("#NAME#", Database.GetName(viewName))
           .Replace("#FIELDS#", fields)
           .Replace("#QUERY#", query).ToString());
    }

    /// <summary>
    /// Create a Index
    /// </summary>
    /// <param name="indexName">index name</param>
    /// <param name="tableName">table name</param>
    /// <param name="expression">index expression</param>
    /// <param name="unique">create index with unique values</param>
    /// <param name="descending">create index with descending order</param>
    public void CreateIndex(string indexName, string tableName, string expression, bool unique, bool descending)
    {
        ExecuteCommand(_getCommand(eCommandString.CreateIndex)
           .Replace("#NAME#", Database.GetName(indexName))
           .Replace("#TABLENAME#", Database.GetName(tableName))
           .Replace("#FIELDS#", expression)
           .Replace("#UNIQUE#", unique ? Database.GetConstant(eDatabaseConstant.unique) : Database.GetConstant(eDatabaseConstant.notunique))
           .Replace("#DESC#", descending ? Database.GetConstant(eDatabaseConstant.desc) : Database.GetConstant(eDatabaseConstant.asc)).ToString());
    }

    /// <summary>
    /// Create a foreign key constraint
    /// </summary>
    /// <param name="constraintName">constraint name</param>
    /// <param name="tableName">table name</param>
    /// <param name="targetTable">target table</param>
    /// <param name="targetField">fieldname in target table (mostly the primary key field)</param>
    /// <param name="fieldName"></param>
    /// <param name="constraintDelete"></param>
    /// <param name="constraintUpdate"></param>
    public void CreateForeignKeyConstraint(string constraintName, string tableName, string fieldName, string targetTable, string targetField, eConstraintOperation constraintUpdate, eConstraintOperation constraintDelete)
    {
        string constraint = "";
        if (constraintUpdate != eConstraintOperation.NoAction)
        {
            constraint += "ON UPDATE ";
            switch (constraintUpdate)
            {
                case eConstraintOperation.SetDefault:
                    constraint += "SET DEFAULT";
                    break;
                case eConstraintOperation.SetNull:
                    constraint += "SET NULL";
                    break;
                case eConstraintOperation.Cascade:
                    constraint += "CASCADE";
                    break;
            }
        }

        if (constraintDelete != eConstraintOperation.NoAction)
        {
            constraint += " ON DELETE ";
            switch (constraintDelete)
            {
                case eConstraintOperation.SetDefault:
                    constraint += "SET DEFAULT";
                    break;
                case eConstraintOperation.SetNull:
                    constraint += "SET NULL";
                    break;
                case eConstraintOperation.Cascade:
                    constraint += "CASCADE";
                    break;
            }
        }

        ExecuteCommand(_getCommand(eCommandString.CreateConstraint)
           .Replace("#NAME#", Database.GetName(constraintName))
           .Replace("#FIELDNAME#", Database.GetName(fieldName))
           .Replace("#TABLENAME#", Database.GetName(tableName))
           .Replace("#TARGETTABLE#", targetTable)
           .Replace("#CONSTRAINT#", constraint)
           .Replace("#TARGETFIELD#", targetField).ToString());
    }


    /// <summary>
    /// Create a field in the database
    /// </summary>
    /// <param name="fieldName">fieldname</param>
    /// <param name="tableName">table name</param>
    /// <param name="fieldType">field type</param>
    /// <param name="fieldsize">field size (only neccessary for string fields - 0 means that string field is a blob field with unlimited size)</param>
    public void CreateField(string fieldName, string tableName, Type fieldType, int fieldsize)
    {
        string fielddef = "";
        if (fieldType.IsEnum)
            fielddef = _getCommand(eCommandString.FieldDefInt32).ToString();
        else if (fieldType == typeof(string))
        {
            if (fieldsize > 0)
            {
                fielddef = _getCommand(eCommandString.FieldDefString)
                    .Replace("#SIZE#", fieldsize.ToString().Trim()).ToString();
            }
            else
                fielddef = _getCommand(eCommandString.FieldDefMemo).ToString();
        }
        else if (fieldType == typeof(int))
            fielddef = _getCommand(eCommandString.FieldDefInt).ToString();
        else if (fieldType == typeof(short))
            fielddef = _getCommand(eCommandString.FieldDefInt16).ToString();
        else if (fieldType == typeof(int))
            fielddef = _getCommand(eCommandString.FieldDefInt32).ToString();
        else if (fieldType == typeof(long))
            fielddef = _getCommand(eCommandString.FieldDefInt64).ToString();
        else if (fieldType == typeof(long))
            fielddef = _getCommand(eCommandString.FieldDefLong).ToString();
        else if (fieldType == typeof(short))
            fielddef = _getCommand(eCommandString.FieldDefShort).ToString();
        else if (fieldType == typeof(double))
            fielddef = _getCommand(eCommandString.FieldDefDouble).ToString();
        else if (fieldType == typeof(float))
            fielddef = _getCommand(eCommandString.FieldDefFloat).ToString();
        else if (fieldType == typeof(byte))
            fielddef = _getCommand(eCommandString.FieldDefByte).ToString();
        else if (fieldType == typeof(decimal))
            fielddef = _getCommand(eCommandString.FieldDefDecimal).ToString();
        else if (fieldType == typeof(bool))
        {
            fielddef = _getCommand(eCommandString.FieldDefBool).ToString()
                .Replace("#SIZE#", "1");
        }
        else if (fieldType == typeof(Type))
            fielddef = _getCommand(eCommandString.FieldDefType).ToString();
        else if (fieldType == typeof(Image))
            fielddef = _getCommand(eCommandString.FieldDefImage).ToString();
        else if (fieldType == typeof(Guid))
        {
            fielddef = _getCommand(eCommandString.FieldDefGuid).ToString()
                .Replace("#SIZE#", "36");
        }
        else if (fieldType == typeof(Guid))
            fielddef = _getCommand(eCommandString.FieldDefObject).ToString();
        else if (fieldType == typeof(DateTime))
            fielddef = _getCommand(eCommandString.FieldDefDateTime).ToString();
        else if (fieldType == typeof(byte[]))
            fielddef = _getCommand(eCommandString.FieldDefBinary).ToString();

        ExecuteCommand(_getCommand(eCommandString.CreateField).Replace("#NAME#", Database.GetName(fieldName)).Replace("#TABLENAME#", Database.GetName(tableName)).Replace("#FIELDOPTIONS#", fielddef).ToString());
    }

    #endregion

    #region tools
    /// <summary>
    /// Enables the default Triggers (BeforeUpdate, BeforeInsert) for the given table.
    /// </summary>
    /// <param name="tableName">Name of the table</param>
    public void EnableDefaultTriggers(string tableName)
    {
        if (ExistTrigger(tableName + "_BI"))
            ExecuteCommand(Database.Translator.GetCommandString(eCommandString.EnableTrigger).Replace("#NAME#", Database.GetName(tableName + "_BI")));

        if (ExistTrigger(tableName + "_BU"))
            ExecuteCommand(Database.Translator.GetCommandString(eCommandString.EnableTrigger).Replace("#NAME#", Database.GetName(tableName + "_BU")));
    }

    /// <summary>
    /// Disables the default Triggers (BeforeUpdate, BeforeInsert) for the given table.
    /// </summary>
    /// <param name="tableName">Name of the table</param>
    public void DisableDefaultTriggers(string tableName)
    {
        if (ExistTrigger(tableName + "_BI"))
            ExecuteCommand(Database.Translator.GetCommandString(eCommandString.DisableTrigger).Replace("#NAME#", Database.GetName(tableName + "_BI")));

        if (ExistTrigger(tableName + "_BU"))
            ExecuteCommand(Database.Translator.GetCommandString(eCommandString.DisableTrigger).Replace("#NAME#", Database.GetName(tableName + "_BU")));
    }

    /// <summary>
    /// Close this connection to the database.
    /// 
    /// Will be rollback a transaction if this transaction was not commited.
    /// </summary>
    public void Close()
    {
        if (Transaction != null)
        {
            Transaction.Rollback();
            Transaction.Dispose();
        }

        Connection?.Dispose();
    }

    /// <summary>
    /// Closes connection and free al unneeded ressource for this connection
    /// </summary>
    public void Dispose()
    {
        Close();
    }


    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="reader"></param>
    /// <param name="fields"></param>
    /// <returns></returns>
    public T ReadFromReader<T>(DbDataReader reader, string[] fields) where T : IDataObject, new()
    {
        return ReadFromReader<T>(reader, typeof(T).GetTypeDescription().Properties.Values
            // ReSharper disable once SuspiciousTypeConversion.Global
            .Select(p => fields.Contains(p.Name)).OfType<PropertyDescription>().ToArray());
    }

    /// <summary>
    /// Prüft ob ein Wert eindeutig ist
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="data"></param>
    /// <param name="field"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    public virtual bool IsUnique<T>(T data, string field, object value) where T : ITable
    {
        bool ret = false;

        TypeDescription desc = data.GetType().GetTypeDescription();

        if (desc.FieldKey == null)
            throw new($"{typeof(T)} does not contain a key field.");

        if (desc.Table == null)
            throw new($"{typeof(T)} is not a table.");

        string query = $"select {desc.FieldKey.Name} from {desc.Table.TableName} where {desc.FieldKey.Name} <> ? and {field} = ?";

        Guid? result = ExecuteCommandScalar<Guid>(query, desc.Accessor[data, desc.FieldKey.Name], value);

        if (result == null || result.Equals(Guid.Empty))
            ret = true;

        return ret;
    }



    #endregion

}
