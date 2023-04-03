
using System.Data.Common;
using System.Text;

namespace AFMapper;

/// <summary>
/// Erstellen von SQL-Abfragen mittels LINQ ähnlicher Syntax
/// </summary>
/// <typeparam name="TModel">Model/Tabelle/View für die die Abfrage erstellt werden soll</typeparam>
public class QueryBuilder<TModel> where TModel : class, new()
{
    private readonly QueueEx<IQueryElement> _elements = new();
    private readonly List<QueryJoin> _joins = new();
    private Type _modelType = typeof(Nullable);
    private TypeDescription? _modelTypeDesc;
    private string[] _fields = { };
    private eQueryType _queryType = eQueryType.Undefined;
    private int _selectTop;
    private readonly string _alias;
    private readonly List<object> _parameters = new();
    private readonly IDatabase _database;

    private TypeDescription modelTypeDesc => _modelTypeDesc ??= _modelType.GetTypeDescription();

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="database"></param>
    /// <param name="alias">Alias der Tabelle/des Views für die die Abfrage erstellt wird. Der ALias muss nur 
    /// angegeben werden wenn mit JOIN, SubQuerys oder ähnlichem gearbeitet wird (=mehrere Tabellen/Views 
    /// involviert sind)
    /// </param>
    public QueryBuilder(IDatabase database, string alias = "")
    {
        _alias = alias;
        _database = database;
    }

    /// <summary>
    /// Quelltext der Abfrage erstellen.
    /// </summary>
    /// <param name="parameters">Array, in dem die Parameter ausgegeben werden.</param>
    /// <returns>Quelltext der Abfrage</returns>
    public string Parse(out object[] parameters)
    {
        StringBuilder bs = new StringBuilder();

        if (_queryType == eQueryType.Select)
        {
            bs.Append("SELECT ");

            if (_selectTop > 0)
            {
                switch (_database.Configuration.DatabaseType)
                {
                    case eDatabaseType.MsSql:
                    case eDatabaseType.AzureSql:
                        bs.Append("TOP ");
                        break;
                    case eDatabaseType.FirebirdEmbeddedSql:
                    case eDatabaseType.FirebirdSql:
                        bs.Append("FIRST ");
                        break;
                }

                bs.Append(_selectTop);
                bs.Append(" ");
            }

            if (_fields.Length == 1 && _fields[0] == "*")
                bs.Append("* ");
            else
            {
                for (int i = 0; i < _fields.Length; i++)
                {
                    bs.Append(_database.GetName(_fields[i]));

                    bs.Append(i < _fields.Length - 1 ? ", " : " ");
                }
            }

            bs.Append("FROM ");

            bs.Append(modelTypeDesc.IsTable ? _database.GetName(modelTypeDesc.Table!.TableName) : _database.GetName(modelTypeDesc.View!.ViewName));

            if (!_alias.IsEmpty())
            {
                bs.Append(" ");
                bs.Append(_alias);
            }

            bs.Append(" ");

            foreach (QueryJoin join in _joins)
                join.Parse(_database, bs, _parameters, this, _alias);

            while (_elements.Count > 0)
            {
                IQueryElement element = _elements.Dequeue();
                element.Parse(_database, bs, _parameters, this, _alias);
            }

            if (_database.Configuration.DatabaseType == eDatabaseType.PostgreSql && _selectTop > 0)
            {
                bs.Append(" LIMIT ");
                bs.Append(_selectTop);
            }
        }
        else if (_queryType == eQueryType.Insert)
        {
            bs.Append("INSERT INTO ");
            bs.Append(_database.GetName(modelTypeDesc.Table!.TableName));

            bs.Append(" ( ");

            for (int i = 0; i < _fields.Length; i++)
            {
                bs.Append(_database.GetName(_fields[i]));
                bs.Append(i < _fields.Length - 1 ? ", " : " ");
            }

            bs.Append(") VALUES ( ");

            for (int i = 0; i < _fields.Length; i++)
            {
                bs.Append("?");
                bs.Append(i < _fields.Length - 1 ? ", " : " ");
            }

            bs.Append(")");
            

            while (_elements.Count > 0)
            {
                IQueryElement element = _elements.Dequeue();
                element.Parse(_database, bs, _parameters, this, _alias);
            }
        }
        else if (_queryType == eQueryType.Update)
        {
            bs.Append("UPDATE ");

            if (_database.Configuration.DatabaseType == eDatabaseType.MsSql || _database.Configuration.DatabaseType == eDatabaseType.AzureSql)
                bs.Append(_alias.IsNotEmpty() ? _alias : _database.GetName(modelTypeDesc.Table!.TableName));
            else
            {
                bs.Append(_database.GetName(modelTypeDesc.Table!.TableName));

                if (!_alias.IsEmpty())
                {
                    bs.Append(" ");
                    bs.Append(_alias);
                }
            }

            bs.Append(" SET ");

            for (int i = 0; i < _fields.Length; i++)
            {
                bs.Append(_database.GetName(_fields[i]));
                bs.Append(" = ? ");

                bs.Append(i < _fields.Length - 1 ? ", " : " ");
            }

            if (_database.Configuration.DatabaseType == eDatabaseType.MsSql || _database.Configuration.DatabaseType == eDatabaseType.AzureSql)
            {
                if (_alias.IsNotEmpty())
                {
                    bs.Append("FROM ");
                    bs.Append(_database.GetName(modelTypeDesc.Table!.TableName));
                    bs.Append(" ");
                }
            }

            while (_elements.Count > 0)
            {
                IQueryElement element = _elements.Dequeue();
                element.Parse(_database, bs, _parameters, this, _alias);
            }
        }
        else if (_queryType == eQueryType.Delete)
        {
            bs.Append("DELETE FROM ");
            
            bs.Append(_database.GetName(modelTypeDesc.Table!.TableName));

            if (!_alias.IsEmpty())
            {
                bs.Append(" ");
                bs.Append(_alias);
            }

            bs.Append(" ");

            while (_elements.Count > 0)
            {
                IQueryElement element = _elements.Dequeue();
                element.Parse(_database, bs, _parameters, this, _alias);
            }
        }


        bs.Replace(" , ", ", ");

        parameters = _parameters.ToArray();

        return bs.ToString();
    }

    /// <summary>
    /// Abfrage parsen und direkt verwendbares DbCommand erstellen.
    /// </summary>
    /// <typeparam name="TCommand">Type des DbCommands (abhängig von der Datenbank)</typeparam>
    /// <typeparam name="TParameter">Type der Parameter des DbCommands (abhängig von der Datenbank)</typeparam>
    /// <returns>das generierte DbCommand-Objekt, dass direkt ausgeführt werden kann</returns>
    public TCommand ParseCommand<TCommand, TParameter>() where TCommand : DbCommand, new() 
                                             where TParameter : DbParameter, new()
    {
        TCommand cmd = new();

        string command = Parse(out var args);

        int cnt = 0;

        if (args.Length > 0)
        {
            foreach (object o in args[0] is IEnumerable<object> ? (IEnumerable<object>)args[0] : args)
            {
                cmd.Parameters.Add(new TParameter { ParameterName = "p" + cnt, Value = _database.Translator.ToDatabase(o) });
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

        cmd.CommandText = _database.Translator.TranslateQuery(ref command);

        return cmd;
    }

    #region CRUD

    /// <summary>
    /// Alle Felder der Tabelle des Views selektieren
    /// </summary>
    public QueryBuilder<TModel> Select()
    {
        return Select("*");
    }

    /// <summary>
    /// Die angegeben Felder der Tabelle/des Views selektieren
    /// 
    /// Der Alias der Tabelle/des Views muss mit dem Feldnamen angegeben werden, wenn es sich NICHT um ein Feld der 
    /// primären Tabelle/des Primären Views handelt. Bei Feldern aus der primären Tabelle/dem primären View wird der 
    /// Alias-Nname automatisch hinzugefügt, wenn dieser im Constructor übergeben wurde.
    /// </summary>
    public QueryBuilder<TModel> Select(params string[] fields)
    {
        _preCheck(eQueryType.Select);
        _fields = fields;

        return this;
    }

    /// <summary>
    /// Die angegeben Felder in einer Tabelle mit neuen Werten füllen.
    /// 
    /// Die zu setzenden Werte werden via 'Values(...)' übergeben. DIe Reihenfolge der Werte muss der Reihenfolge 
    /// der Felder entsprechen.
    /// </summary>
    /// <param name="fields">Felder/Spalten, die beschreiben werden sollen</param>
    public QueryBuilder<TModel> Update(params string[] fields)
    {
        _preCheck(eQueryType.Update);

        if (fields == null || fields.Length < 1)
            throw new InvalidOperationException("Zu aktualisierende Felder müssen für Update angegeben werden.");

        _fields = fields;

        return this;
    }

    /// <summary>
    /// Die angegeben Felder in einer Tabelle mit neuen Werten füllen.
    /// 
    /// Die zu setzenden Werte werden via 'Values(...)' übergeben. DIe Reihenfolge der Werte muss der Reihenfolge 
    /// der Felder entsprechen.
    /// </summary>
    /// <param name="fields">Felder/Spalten, die beschreiben werden sollen</param>
    public QueryBuilder<TModel> Insert(params string[] fields)
    {
        _preCheck(eQueryType.Insert);

        if (fields == null || fields.Length < 1)
            throw new InvalidOperationException("Zu beschreibende Felder müssen für Insert angegeben werden.");

        _fields = fields;

        return this;
    }

    /// <summary>
    /// Datensätze aus einer Tabelle löschen.
    /// </summary>
    public QueryBuilder<TModel> Delete()
    {
        _preCheck(eQueryType.Delete);

        return this;
    }

    #endregion

    /// <summary>
    /// Nur die ersten N Datensätze ermitteln
    /// </summary>
    /// <param name="records">Anzahl der Datensätze</param>
    public QueryBuilder<TModel> Top(int records = 1)
    {
        if (_queryType != eQueryType.Select)
            throw new InvalidOperationException("Top kann nur mit Select verwendet werden.");

        if (records < 1)
            throw new ArgumentException("Anzahl der Datensätze muss > 0 sein.");

        if (_selectTop > 0)
            throw new InvalidOperationException("Top kann nicht mehrfach verwendet werden.");

        _selectTop = records;

        return this;
    }

    /// <summary>
    /// Werte für Insert oder Update.
    ///
    /// Die Anzahl und Reihenfolge der Werte muss der Anzahl und Reihenfolge der Felder entsprechen.
    /// </summary>
    /// <param name="values">Werte</param>
    /// <exception cref="InvalidOperationException">Anzahl stimmt nicht überein oder wes ist kein Insert und kein Update Query</exception>
    public QueryBuilder<TModel> Values(params object[] values)
    {
        if (_queryType != eQueryType.Update && _queryType != eQueryType.Insert)
            throw new InvalidOperationException("Values können nur bei Insert oder Update angegeben werden.");

        if (_fields.Length != values.Length)
            throw new InvalidOperationException("Anzahl der Values entspricht nicht der Anzahl der angegeben Felder.");

        _parameters.AddRange(values);

        return this;
    }


    /// <summary>
    /// WHERE-Bedingung für die Abfrage (field = value)
    ///
    /// Als Operator wird = verwendet.
    /// </summary>
    /// <param name="field">Feld/Spalte</param>
    /// <param name="value">Wert der Spalte</param>
    public QueryBuilder<TModel> Where(string field, object value)
    {
        return Where(field, "=", value);
    }

    /// <summary>
    /// WHERE-Bedingung für die Abfrage (field operation value, z.B. field >= value)
    /// </summary>
    /// <param name="field">Feld/Spalte</param>
    /// <param name="value">Wert der Spalte</param>
    /// <param name="operation">Operator für den Vergleich (=, &lt;, &gt; etc.)</param>
    public QueryBuilder<TModel> Where(string field, string operation, object value)
    {
        _addWhere(eBooleanConnector.None, field, operation, value);

        return this;
    }

    /// <summary>
    /// WHERE-Bedingung in vollständiger SQL-Syntax. Über RAW-Bedingungen lassen sich zum Beispiel Funktionen beim 
    /// Vergleich nutzen. Bsp:
    /// ...WhereRaw("CRUpper(BEN_VORNAME) = ? or CRUpper(BEN_VORNAME) = ?", "EMIL", "HANS");
    /// 
    /// </summary>
    /// <param name="expression">vollst. SQL-Ausdruck</param>
    /// <param name="values">Werte für den Vergleich</param>
    public QueryBuilder<TModel> WhereRaw(string expression, params object[] values)
    {
        _addWhereRaw(eBooleanConnector.None, expression, values);

        return this;
    }

    /// <summary>
    /// DIESE METHODE WIRD NICHT DIREKT VERWENDEN!
    ///
    /// Die Methode wird intern benötigt um verschachtelte Bedingungen zu erzeugen.
    /// </summary>
    /// <param name="callback">die Callback Funktion</param>
    public QueryBuilder<TModel> Where(Func<QueryWhere, QueryWhere> callback)
    {
        _addWhereSub(eBooleanConnector.None, callback);

        return this;
    }

    /// <summary>
    /// AND-Bedingung für die Abfrage ( and field = value)
    ///
    /// Als Operator wird = verwendet.
    /// </summary>
    /// <param name="field">Feld/Spalte</param>
    /// <param name="value">Wert der Spalte</param>
    public QueryBuilder<TModel> And(string field, object value)
    {
        return And(field, "=", value);
    }

    /// <summary>
    /// AND-Bedingung für die Abfrage (field operation value, z.B. and field >= value)
    /// </summary>
    /// <param name="field">Feld/Spalte</param>
    /// <param name="value">Wert der Spalte</param>
    /// <param name="operation">Operator für den Vergleich (=, &lt;, &gt; etc.)</param>
    public QueryBuilder<TModel> And(string field, string operation, object value)
    {
        _addWhere(eBooleanConnector.And, field, operation, value);

        return this;
    }

    /// <summary>
    /// AND-Bedingung in vollständiger SQL-Syntax. Über RAW-Bedingungen lassen sich zum Beispiel Funktionen beim 
    /// Vergleich nutzen. Bsp:
    /// ...AndRaw("CRUpper(BEN_VORNAME) = ? or CRUpper(BEN_VORNAME) = ?", "EMIL", "HANS");
    /// 
    /// </summary>
    /// <param name="expression">vollst. SQL-Ausdruck</param>
    /// <param name="values">Werte für den Vergleich</param>
    public QueryBuilder<TModel> AndRaw(string expression, params object[] values)
    {
        _addWhereRaw(eBooleanConnector.And, expression, values);

        return this;
    }
    
    /// <summary>
    /// DIESE METHODE WIRD NICHT DIREKT VERWENDEN!
    ///
    /// Die Methode wird intern benötigt um verschachtelte Bedingungen zu erzeugen.
    /// </summary>
    /// <param name="callback">die Callback Funktion</param>
    public QueryBuilder<TModel> And(Func<QueryWhere, QueryWhere> callback)
    {
        _addWhereSub(eBooleanConnector.And, callback);

        return this;
    }

    /// <summary>
    /// OR-Bedingung für die Abfrage ( or field = value)
    ///
    /// Als Operator wird = verwendet.
    /// </summary>
    /// <param name="field">Feld/Spalte</param>
    /// <param name="value">Wert der Spalte</param>
    public QueryBuilder<TModel> Or(string field, object value)
    {
        return Or(field, "=", value);
    }

    /// <summary>
    /// OR-Bedingung für die Abfrage (field operation value, z.B. or field >= value)
    /// </summary>
    /// <param name="field">Feld/Spalte</param>
    /// <param name="value">Wert der Spalte</param>
    /// <param name="operation">Operator für den Vergleich (=, &lt;, &gt; etc.)</param>
    public QueryBuilder<TModel> Or(string field, string operation, object value)
    {
        _addWhere(eBooleanConnector.Or, field, operation, value);

        return this;
    }

    /// <summary>
    /// OR-Bedingung in vollständiger SQL-Syntax. Über RAW-Bedingungen lassen sich zum Beispiel Funktionen beim 
    /// Vergleich nutzen. Bsp:
    /// ...AndRaw("CRUpper(BEN_VORNAME) = ? or CRUpper(BEN_VORNAME) = ?", "EMIL", "HANS");
    /// 
    /// </summary>
    /// <param name="expression">vollst. SQL-Ausdruck</param>
    /// <param name="values">Werte für den Vergleich</param>
    public QueryBuilder<TModel> OrRaw(string expression, params object[] values)
    {
        _addWhereRaw(eBooleanConnector.Or, expression, values);

        return this;
    }

    /// <summary>
    /// DIESE METHODE WIRD NICHT DIREKT VERWENDEN!
    ///
    /// Die Methode wird intern benötigt um verschachtelte Bedingungen zu erzeugen.
    /// </summary>
    /// <param name="callback">die Callback Funktion</param>
    public QueryBuilder<TModel> Or(Func<QueryWhere, QueryWhere> callback)
    {
        _addWhereSub(eBooleanConnector.Or, callback);

        return this;
    }

    /// <summary>
    /// AND NOT -Bedingung für die Abfrage ( and not field = value)
    ///
    /// Als Operator wird = verwendet.
    /// </summary>
    /// <param name="field">Feld/Spalte</param>
    /// <param name="value">Wert der Spalte</param>
    public QueryBuilder<TModel> AndNot(string field, object value)
    {
        return AndNot(field, "=", value);
    }

    /// <summary>
    /// AND NOT-Bedingung für die Abfrage (field operation value, z.B. and not field >= value)
    /// </summary>
    /// <param name="field">Feld/Spalte</param>
    /// <param name="value">Wert der Spalte</param>
    /// <param name="operation">Operator für den Vergleich (=, &lt;, &gt; etc.)</param>
    public QueryBuilder<TModel> AndNot(string field, string operation, object value)
    {
        _addWhere(eBooleanConnector.AndNot, field, operation, value);

        return this;
    }

    /// <summary>
    /// AND NOT-Bedingung in vollständiger SQL-Syntax. Über RAW-Bedingungen lassen sich zum Beispiel Funktionen beim 
    /// Vergleich nutzen. Bsp:
    /// ...AndRaw("CRUpper(BEN_VORNAME) = ? or CRUpper(BEN_VORNAME) = ?", "EMIL", "HANS");
    /// 
    /// </summary>
    /// <param name="expression">vollst. SQL-Ausdruck</param>
    /// <param name="values">Werte für den Vergleich</param>
    public QueryBuilder<TModel> AndNotRaw(string expression, params object[] values)
    {
        _addWhereRaw(eBooleanConnector.AndNot, expression, values);

        return this;
    }

    /// <summary>
    /// DIESE METHODE WIRD NICHT DIREKT VERWENDEN!
    ///
    /// Die Methode wird intern benötigt um verschachtelte Bedingungen zu erzeugen.
    /// </summary>
    /// <param name="callback">die Callback Funktion</param>
    public QueryBuilder<TModel> AndNot(Func<QueryWhere, QueryWhere> callback)
    {
        _addWhereSub(eBooleanConnector.AndNot, callback);

        return this;
    }

    /// <summary>
    /// AND NOT -Bedingung für die Abfrage ( or not field = value)
    ///
    /// Als Operator wird = verwendet.
    /// </summary>
    /// <param name="field">Feld/Spalte</param>
    /// <param name="value">Wert der Spalte</param>
    public QueryBuilder<TModel> OrNot(string field, object value)
    {
        return OrNot(field, "=", value);
    }

    /// <summary>
    /// OR NOT-Bedingung für die Abfrage (field operation value, z.B. or not field >= value)
    /// </summary>
    /// <param name="field">Feld/Spalte</param>
    /// <param name="value">Wert der Spalte</param>
    /// <param name="operation">Operator für den Vergleich (=, &lt;, &gt; etc.)</param>
    public QueryBuilder<TModel> OrNot(string field, string operation, object value)
    {
        _addWhere(eBooleanConnector.OrNot, field, operation, value);

        return this;
    }

    /// <summary>
    /// OR NOT-Bedingung in vollständiger SQL-Syntax. Über RAW-Bedingungen lassen sich zum Beispiel Funktionen beim 
    /// Vergleich nutzen. Bsp:
    /// ...AndRaw("CRUpper(BEN_VORNAME) = ? or CRUpper(BEN_VORNAME) = ?", "EMIL", "HANS");
    /// 
    /// </summary>
    /// <param name="expression">vollst. SQL-Ausdruck</param>
    /// <param name="values">Werte für den Vergleich</param>
    public QueryBuilder<TModel> OrNotRaw(string expression, params object[] values)
    {
        _addWhereRaw(eBooleanConnector.OrNot, expression, values);

        return this;
    }

    /// <summary>
    /// DIESE METHODE WIRD NICHT DIREKT VERWENDEN!
    ///
    /// Die Methode wird intern benötigt um verschachtelte Bedingungen zu erzeugen.
    /// </summary>
    /// <param name="callback">die Callback Funktion</param>
    public QueryBuilder<TModel> OrNot(Func<QueryWhere, QueryWhere> callback)
    {
        _addWhereSub(eBooleanConnector.OrNot, callback);

        return this;
    }

    /// <summary>
    /// Gruppieren der Daten nach dem angegebenen Feld.
    /// </summary>
    /// <param name="field">Feldname</param>
    public QueryBuilder<TModel> GroupBy(string field)
    {
        if (_queryType != eQueryType.Select)
            throw new InvalidOperationException("GroupBy kann nur bei Select angegeben werden.");

        if (_elements.Count < 1)
            throw new InvalidOperationException("GroupBy kann an dieser Stelle nicht verwendet werden.");

        _elements.Enqueue(new QueryGroup(field, eBooleanConnector.None));

        return this;
    }

    /// <summary>
    /// Weitere Gruppierung/Sortierung der Daten nach dem angegebenen Feld.
    ///
    /// Kann nur nach GroupBy, OrderBy, OrderDescBy, ThenBy oder ThenDescBy verwendet werden.
    /// </summary>
    /// <param name="field">Feldname</param>
    public QueryBuilder<TModel> ThenBy(string field)
    {
        if (_queryType != eQueryType.Select)
            throw new InvalidOperationException("ThenBy kann nur bei Select angegeben werden.");

        if (_elements.Count < 1)
        {
            throw new InvalidOperationException(
                "ThenBy kann nur nach GroupBy, SortBy, ThenBy oder ThenDescBy verwendet werden.");
        }

        if (_elements.PeekLast() is QueryGroup)
            _elements.Enqueue(new QueryGroup(field, eBooleanConnector.And));
        else if (_elements.PeekLast() is QuerySort)
            _elements.Enqueue(new QuerySort(field, eSortMode.Ascending, eBooleanConnector.And));
        else
        {
            throw new InvalidOperationException(
                "ThenBy kann nur nach GroupBy, SortBy, ThenBy oder ThenDescBy verwendet werden.");
        }

        return this;
    }

    /// <summary>
    /// Sortieren (aufsteigend) der Daten nach dem angegebenen Feld.
    /// </summary>
    /// <param name="field">Feldname</param>
    public QueryBuilder<TModel> OrderBy(string field)
    {
        return _sortBy(field, eSortMode.Ascending);
    }

    /// <summary>
    /// Sortieren (absteigend) der Daten nach dem angegebenen Feld.
    /// </summary>
    /// <param name="field">Feldname</param>
    public QueryBuilder<TModel> OrderDescBy(string field)
    {
        return _sortBy(field, eSortMode.Descending);
    }

    /// <summary>
    /// Weitere Sortierung der Daten nach dem angegebenen Feld.
    ///
    /// Kann nur nach OrderBy, OrderDescBy, ThenBy oder ThenDescBy verwendet werden.
    /// </summary>
    /// <param name="field">Feldname</param>
    public QueryBuilder<TModel> ThenDescBy(string field)
    {
        return _thenSortBy(field, eSortMode.Descending);
    }

    /// <summary>
    /// Join (Left Join) mit einer anderen Tabelle/einem anderen View
    /// </summary>
    /// <typeparam name="TModelJoin">Typ der die Tabelle/den View repräsentiert</typeparam>
    /// <param name="alias">Aliasname für die zu verbindenede Tabelle/den zu verbindenden View</param>
    /// <param name="sourcefield">Feld in der Ausgangstabelle/dem Ausgangsview</param>
    /// <param name="targetfield">Feld in der zu verbindenden Tabelle/dem zu verbindenden View</param>
    public QueryBuilder<TModel> LeftJoin<TModelJoin>(string alias, string sourcefield, string targetfield) where TModelJoin : class
    {
        _join(typeof(TModelJoin), eJoinMode.Left, alias, sourcefield, targetfield);

        return this;
    }

    /// <summary>
    /// Join (Right Join) mit einer anderen Tabelle/einem anderen View
    /// </summary>
    /// <typeparam name="TModelJoin">Typ der die Tabelle/den View repräsentiert</typeparam>
    /// <param name="alias">Aliasname für die zu verbindenede Tabelle/den zu verbindenden View</param>
    /// <param name="sourcefield">Feld in der Ausgangstabelle/dem Ausgangsview</param>
    /// <param name="targetfield">Feld in der zu verbindenden Tabelle/dem zu verbindenden View</param>
    public QueryBuilder<TModel> RightJoin<TModelJoin>(string alias, string sourcefield, string targetfield) where TModelJoin : class
    {
        _join(typeof(TModelJoin), eJoinMode.Right, alias, sourcefield, targetfield);

        return this;
    }

    /// <summary>
    /// Join (Inner Join) mit einer anderen Tabelle/einem anderen View
    /// </summary>
    /// <typeparam name="TModelJoin">Typ der die Tabelle/den View repräsentiert</typeparam>
    /// <param name="alias">Aliasname für die zu verbindenede Tabelle/den zu verbindenden View</param>
    /// <param name="sourcefield">Feld in der Ausgangstabelle/dem Ausgangsview</param>
    /// <param name="targetfield">Feld in der zu verbindenden Tabelle/dem zu verbindenden View</param>
    public QueryBuilder<TModel> InnerJoin<TModelJoin>(string alias, string sourcefield, string targetfield) where TModelJoin : class
    {
        _join(typeof(TModelJoin), eJoinMode.Inner, alias, sourcefield, targetfield);

        return this;
    }

    /// <summary>
    /// Join (Full Join) mit einer anderen Tabelle/einem anderen View
    /// </summary>
    /// <typeparam name="TModelJoin">Typ der die Tabelle/den View repräsentiert</typeparam>
    /// <param name="alias">Aliasname für die zu verbindenede Tabelle/den zu verbindenden View</param>
    /// <param name="sourcefield">Feld in der Ausgangstabelle/dem Ausgangsview</param>
    /// <param name="targetfield">Feld in der zu verbindenden Tabelle/dem zu verbindenden View</param>
    public QueryBuilder<TModel> FullJoin<TModelJoin>(string alias, string sourcefield, string targetfield) where TModelJoin : class
    {
        _join(typeof(TModelJoin), eJoinMode.Full, alias, sourcefield, targetfield);

        return this;
    }

    /// <summary>
    /// Join (Left Join) mit einer anderen Tabelle/einem anderen View
    /// </summary>
    /// <typeparam name="TModelJoin">Typ der die Tabelle/den View repräsentiert</typeparam>
    /// <param name="alias">Aliasname für die zu verbindenede Tabelle/den zu verbindenden View</param>
    /// <param name="sourcefield">Feld in der Ausgangstabelle/dem Ausgangsview</param>
    /// <param name="targetfield">Feld in der zu verbindenden Tabelle/dem zu verbindenden View</param>
    public QueryBuilder<TModel> Join<TModelJoin>(string alias, string sourcefield, string targetfield) where TModelJoin : class
    {

        _join(typeof(TModelJoin), eJoinMode.Left, alias, sourcefield, targetfield);

        return this;
    }

    /// <summary>
    /// Join (Left Join) mit einer anderen Tabelle/einem anderen View
    ///
    /// Die Definition der Verknüpfung erfolgt dabei in reinem SQL Code.
    /// </summary>
    /// <typeparam name="TModelJoin">Typ der die Tabelle/den View repräsentiert</typeparam>
    /// <param name="alias">Aliasname für die zu verbindenede Tabelle/den zu verbindenden View</param>
    /// <param name="expression">SQL Code, der die Verbindung definiert (Bsp: CRUpper(SRCFIELD_NAME) = CRUpper(TARFIELD_NAME))</param>
    public QueryBuilder<TModel> LeftJoin<TModelJoin>(string alias, string expression) where TModelJoin : class
    {
        _joinRaw(typeof(TModelJoin), eJoinMode.Left, alias, expression);

        return this;
    }

    /// <summary>
    /// Join (Right Join) mit einer anderen Tabelle/einem anderen View
    ///
    /// Die Definition der Verknüpfung erfolgt dabei in reinem SQL Code.
    /// </summary>
    /// <typeparam name="TModelJoin">Typ der die Tabelle/den View repräsentiert</typeparam>
    /// <param name="alias">Aliasname für die zu verbindenede Tabelle/den zu verbindenden View</param>
    /// <param name="expression">SQL Code, der die Verbindung definiert (Bsp: CRUpper(SRCFIELD_NAME) = CRUpper(TARFIELD_NAME))</param>
    public QueryBuilder<TModel> RightJoin<TModelJoin>(string alias, string expression) where TModelJoin : class
    {
        _joinRaw(typeof(TModelJoin), eJoinMode.Left, alias, expression);

        return this;
    }

    /// <summary>
    /// Join (Inner Join) mit einer anderen Tabelle/einem anderen View
    ///
    /// Die Definition der Verknüpfung erfolgt dabei in reinem SQL Code.
    /// </summary>
    /// <typeparam name="TModelJoin">Typ der die Tabelle/den View repräsentiert</typeparam>
    /// <param name="alias">Aliasname für die zu verbindenede Tabelle/den zu verbindenden View</param>
    /// <param name="expression">SQL Code, der die Verbindung definiert (Bsp: CRUpper(SRCFIELD_NAME) = CRUpper(TARFIELD_NAME))</param>
    public QueryBuilder<TModel> InnerJoin<TModelJoin>(string alias, string expression) where TModelJoin : class
    {
        _joinRaw(typeof(TModelJoin), eJoinMode.Left, alias, expression);

        return this;
    }

    /// <summary>
    /// Join (Full Join) mit einer anderen Tabelle/einem anderen View
    ///
    /// Die Definition der Verknüpfung erfolgt dabei in reinem SQL Code.
    /// </summary>
    /// <typeparam name="TModelJoin">Typ der die Tabelle/den View repräsentiert</typeparam>
    /// <param name="alias">Aliasname für die zu verbindenede Tabelle/den zu verbindenden View</param>
    /// <param name="expression">SQL Code, der die Verbindung definiert (Bsp: CRUpper(SRCFIELD_NAME) = CRUpper(TARFIELD_NAME))</param>
    public QueryBuilder<TModel> FullJoin<TModelJoin>(string alias, string expression) where TModelJoin : class
    {
        _joinRaw(typeof(TModelJoin), eJoinMode.Left, alias, expression);

        return this;
    }

    /// <summary>
    /// Join (Left Join) mit einer anderen Tabelle/einem anderen View
    ///
    /// Die Definition der Verknüpfung erfolgt dabei in reinem SQL Code.
    /// </summary>
    /// <typeparam name="TModelJoin">Typ der die Tabelle/den View repräsentiert</typeparam>
    /// <param name="alias">Aliasname für die zu verbindenede Tabelle/den zu verbindenden View</param>
    /// <param name="expression">SQL Code, der die Verbindung definiert (Bsp: CRUpper(SRCFIELD_NAME) = CRUpper(TARFIELD_NAME))</param>
    public QueryBuilder<TModel> Join<TModelJoin>(string alias, string expression) where TModelJoin : class
    {

        _joinRaw(typeof(TModelJoin), eJoinMode.Left, alias, expression);

        return this;
    }

    internal void _join(Type modelType, eJoinMode mode, string alias, string sourcefield, string targetfield)
    {
        if (_queryType != eQueryType.Select)
            throw new InvalidOperationException("Join kann nur bei Select angegeben werden.");


        if (alias == _alias || _joins.FirstOrDefault(j => j.Alias == alias) != null)
            throw new InvalidOperationException($"Der Alias {alias} wird bereits verwendet.");

        TypeDescription tdesc = modelType.GetTypeDescription();

        if (tdesc == null)
        {
            throw new InvalidOperationException(
                $"Der Typ {modelType} kann im Join nicht verwendet werden. Es gibt keine Typbeschreibung.");
        }

        if (!tdesc.IsTable && !tdesc.IsView)
        {
            throw new InvalidOperationException(
                $"Der Typ {modelType} ist weder Tabelle noch View und kann deswegen nicht im Join verwendet werden.");
        }

        _joins.Add(new QueryJoin(mode, alias, modelType)
        {
            SourceField = sourcefield,
            TargetField = targetfield
        });
    }

    internal void _joinRaw(Type modelType, eJoinMode mode, string alias, string expression)
    {
        if (_queryType != eQueryType.Select)
            throw new InvalidOperationException("Join kann nur bei Select angegeben werden.");


        if (alias == _alias || _joins.FirstOrDefault(j => j.Alias == alias) != null)
            throw new InvalidOperationException($"Der Alias {alias} wird bereits verwendet.");

        TypeDescription tdesc = modelType.GetTypeDescription();

        if (tdesc == null)
        {
            throw new InvalidOperationException(
                $"Der Typ {modelType} kann im Join nicht verwendet werden. Es gibt keine Typbeschreibung.");
        }

        if (!tdesc.IsTable && !tdesc.IsView)
        {
            throw new InvalidOperationException(
                $"Der Typ {modelType} ist weder Tabelle noch View und kann deswegen nicht im Join verwendet werden.");
        }

        _joins.Add(new QueryJoin(mode, alias, modelType)
        {
            ExpressionRaw = expression
        });
    }

    internal QueryBuilder<TModel> _sortBy(string field, eSortMode mode)
    {
        if (_queryType != eQueryType.Select)
            throw new InvalidOperationException("GroupBy kann nur bei Select angegeben werden.");

        if (_elements.Count < 1)
            throw new InvalidOperationException("GroupBy kann an dieser Stelle nicht verwendet werden.");

        _elements.Enqueue(new QuerySort(field, mode, eBooleanConnector.None));

        return this;
    }

    internal QueryBuilder<TModel> _thenSortBy(string field, eSortMode mode)
    {
        if (_queryType != eQueryType.Select)
            throw new InvalidOperationException("ThenBy kann nur bei Select angegeben werden.");

        if (_elements.Count < 1 || !(_elements.PeekLast() is QuerySort))
            throw new InvalidOperationException("ThenBy kann nur nach GroupBy oder ThenBy verwendet werden.");

        _elements.Enqueue(new QuerySort(field, mode, eBooleanConnector.And));
        
        return this;
    }

    private void _addWhere(eBooleanConnector connector, string field, string operation, object value)
    {
        if (connector == eBooleanConnector.None && _queryType == eQueryType.Undefined)
            throw new InvalidOperationException("Where kann nur nach Select, Update oder Delete verwendet werden.");

        if (connector == eBooleanConnector.None && _elements.Count > 0)
        {
            throw new InvalidOperationException(
                "Where kann nicht mehrfach in einer Ebene verwendet werden. Nutzen Sie stattdessen And, Or u.ä..");
        }

        if (connector != eBooleanConnector.None && (_elements.Count < 1 || !(_elements.PeekLast() is QueryWhere)))
            throw new InvalidOperationException($"{connector} kann nur nach einem Where verwendet werden.");

        _elements.Enqueue(new QueryWhere(field, new[] { value }, operation, connector));
    }

    private void _addWhereRaw(eBooleanConnector connector, string expression, params object[] values)
    {
        if (connector == eBooleanConnector.None && _queryType == eQueryType.Undefined)
            throw new InvalidOperationException("Where kann nur nach Select, Update oder Delete verwendet werden.");

        if (connector == eBooleanConnector.None && _elements.Count > 0)
        {
            throw new InvalidOperationException(
                "Where kann nicht mehrfach in einer Ebene verwendet werden. Nutzen Sie stattdessen And, Or u.ä..");
        }

        if (connector != eBooleanConnector.None && (_elements.Count < 1 || !(_elements.PeekLast() is QueryWhere)))
            throw new InvalidOperationException($"{connector} kann nur nach einem Where verwendet werden.");

        _elements.Enqueue(new QueryWhere(expression, values, connector));
    }

    private void _addWhereSub(eBooleanConnector connector, Func<QueryWhere, QueryWhere> callback)
    {
        if (connector == eBooleanConnector.None && _queryType == eQueryType.Undefined)
            throw new InvalidOperationException("Where kann nur nach Select, Update oder Delete verwendet werden.");

        if (connector == eBooleanConnector.None && _elements.Count > 0)
        {
            throw new InvalidOperationException(
                "Where kann nicht mehrfach in einer Ebene verwendet werden. Nutzen Sie stattdessen And, Or u.ä..");
        }

        if (connector != eBooleanConnector.None && (_elements.Count < 1 || !(_elements.PeekLast() is QueryWhere)))
            throw new InvalidOperationException($"{connector} kann nur nach einem Where verwendet werden.");

        QueryWhere where = new QueryWhere(connector);
        callback(where);

        _elements.Enqueue(where);
    }

    private void _preCheck(eQueryType type)
    {
        _modelType = typeof(TModel);
        
        if (_queryType != eQueryType.Undefined)
            throw new InvalidOperationException($"{type} kann nicht nach Select, Insert, Update oder Delete verwendet werden.");


        _queryType = type;

        if (modelTypeDesc == null)
        {
            throw new InvalidOperationException(
                $"Für den Typen {_modelType} ist keine TypeDescription verfügbar. QueryBuilder kann mit dem Typen nicht verwendet werden.");
        }

        if (!modelTypeDesc.IsTable && !modelTypeDesc.IsView)
        {
            throw new InvalidOperationException(
                $"Der Typ {_modelType} ist weder als Tabelle noch als View gekennzeichnet. QueryBuilder kann mit dem Typen nicht verwendet werden.");
        }

        if (modelTypeDesc.IsView && (type == eQueryType.Insert || type == eQueryType.Update || type == eQueryType.Delete))
        {
            throw new InvalidOperationException(
                $"Update oder Delete sind für den Typen {_modelType} nicht verfügbar, weil der Typ ein View repräsentiert (nur Select ist verfügbar).");
        }
    }

    internal enum eQueryType
    {
        Undefined,
        Select,
        Update,
        Delete,
        Insert
    }

    internal enum eBooleanConnector
    {
        None,
        And,
        Or,
        AndNot,
        OrNot
    }

    internal enum eSortMode
    {
        Ascending,
        Descending
    }

    internal enum eJoinMode
    {
        Left,
        Right,
        Inner,
        Full
    }

    /// <summary>
    /// Beschreibt ein Element in QueryBuilder (Where, Join, GoupBy etc.)
    /// </summary>
    public interface IQueryElement
    {
        void Parse(IDatabase database, StringBuilder bs, List<object> parametersList, object parent, string alias);

    }

    /// <summary>
    /// Basiselement aller Elemente in QueryBuilder
    /// </summary>
    public abstract class QueryElement : IQueryElement
    {
        public virtual void Parse(IDatabase database, StringBuilder bs, List<object> parametersList, object parent, string alias)
        {
        }

    }

    /// <summary>
    /// Ein Join-Element
    /// </summary>
    public class QueryJoin : QueryElement
    {
        internal string SourceField { get; set; } = "";
        internal string TargetField { get; set; } = "";
        internal string? ExpressionRaw { get; set; }

        internal Type ModelType { get; }

        internal TypeDescription ModelTypeDesc { get; }

        internal string Alias { get; }

        internal eJoinMode Mode { get; }

        internal QueryJoin(eJoinMode mode, string alias, Type modelType)
        {
            Mode = mode;
            Alias  = alias;
            ModelType = modelType;
            ModelTypeDesc = modelType.GetTypeDescription();
        }


        public override void Parse(IDatabase database, StringBuilder bs, List<object> parametersList, object parent, string alias)
        {
            switch (Mode)
            {
                case eJoinMode.Left:
                    bs.Append("LEFT JOIN ");
                    break;
                case eJoinMode.Right:
                    bs.Append("RIGHT JOIN ");
                    break;
                case eJoinMode.Inner:
                    bs.Append("INNER JOIN ");
                    break;
                case eJoinMode.Full:
                    bs.Append("FULL OUTER JOIN ");
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }


            bs.Append(ModelTypeDesc.IsTable ? database.GetName(ModelTypeDesc.Table!.TableName) : database.GetName(ModelTypeDesc.View!.ViewName));

            bs.Append(" ");
            bs.Append(Alias);

            bs.Append(" ON ");

            if (ExpressionRaw.IsEmpty())
            {
                if (alias.IsNotEmpty() && !SourceField.Contains("."))
                {
                    bs.Append(alias);
                    bs.Append(".");
                }

                bs.Append(database.GetName(SourceField));
                bs.Append(" = ");
                if (Alias.IsNotEmpty() && !TargetField.Contains("."))
                {
                    bs.Append(Alias);
                    bs.Append(".");
                }

                bs.Append(database.GetName(TargetField));
                bs.Append(" ");
            }
            else
            {
                bs.Append(ExpressionRaw);
                bs.Append(" ");
            }
        }
    }

    /// <summary>
    /// Ein Group-Element
    /// </summary>
    public class QueryGroup : QueryElement
    {
        internal string Field { get; }
        internal eBooleanConnector Connector { get; }

        internal QueryGroup(string field, eBooleanConnector connector)
        {
            Field = field;
            Connector = connector;
        }

        public override void Parse(IDatabase database, StringBuilder bs, List<object> parametersList, object parent, string alias)
        {
            if (Connector == eBooleanConnector.None)
            {
                bs.Append("GROUP BY ");
                if (alias.IsNotEmpty() && !Field.Contains("."))
                {
                    bs.Append(alias);
                    bs.Append(".");
                }
            }
            else
            {
                bs.Append(", ");
                if (alias.IsNotEmpty() && !Field.Contains("."))
                {
                    bs.Append(alias);
                    bs.Append(".");
                }
            }

            bs.Append(database.GetName(Field));
            bs.Append(" ");
        }
    }

    /// <summary>
    /// Ein Sort-Element
    /// </summary>
    public class QuerySort : QueryElement
    {
        internal string Field { get; }

        internal eBooleanConnector Connector { get; }

        internal eSortMode SortMode { get; }

        internal QuerySort(string field, eSortMode sortMode, eBooleanConnector connector)
        {
            Field = field;
            Connector = connector;
            SortMode = sortMode;
        }

        public override void Parse(IDatabase database, StringBuilder bs, List<object> parametersList, object parent, string alias)
        {
            if (Connector == eBooleanConnector.None)
            {
                bs.Append("ORDER BY ");

                if (alias.IsNotEmpty() && !Field.Contains("."))
                {
                    bs.Append(alias);
                    bs.Append(".");
                }
            }
            else
            {
                bs.Append(", ");

                if (alias.IsNotEmpty() && !Field.Contains("."))
                {
                    bs.Append(alias);
                    bs.Append(".");
                }
            }

            bs.Append(database.GetName(Field));
            bs.Append(" ");

            if (SortMode == eSortMode.Descending)
                bs.Append("DESC ");
        }
    }

    /// <summary>
    /// Ein Where-Element
    /// </summary>
    public class QueryWhere : QueryElement
    {
        private readonly QueueEx<IQueryElement> _elements = new();

        internal string Field { get; }
        internal object[] Value { get; }
        internal string Operator { get; }
        internal string? RawExpression { get; init; }

        internal eBooleanConnector Connector { get; }

        internal QueryWhere(string field, object[] value, string op, eBooleanConnector connector)
        {
            Field = field;
            Value = value;
            Operator = op;
            Connector = connector;
        }

        internal QueryWhere(string expression, object[] value, eBooleanConnector connector)
        {
            Field = "";
            RawExpression = expression;
            Value = value;
            Operator = "";
            Connector = connector;
        }

        internal QueryWhere(eBooleanConnector connector)
        {
            Field = "";
            RawExpression = "";
            Value = Array.Empty<object>();
            Operator = "";
            Connector = connector;
        }

        public override void Parse(IDatabase database, StringBuilder bs, List<object> parametersList, object parent, string alias)
        {
            if (Value.Length > 0)
                parametersList.AddRange(Value);
            
            if (Connector == eBooleanConnector.None)
            {
                if (parent is QueryWhere)
                {
                    if (_elements.Count > 0)
                    {

                        bs.Append("( ");

                        while (_elements.Count > 0)
                        {
                            IQueryElement element = _elements.Dequeue();
                            element.Parse(database, bs, parametersList, this, alias);
                        }

                        bs.Append(") ");
                    }
                }
                else
                    bs.Append("WHERE ");
            }
            else if (Connector == eBooleanConnector.And)
                bs.Append("AND ");
            else if (Connector == eBooleanConnector.Or)
                bs.Append("OR ");
            else if (Connector == eBooleanConnector.AndNot)
                bs.Append("AND NOT ");
            else if (Connector == eBooleanConnector.OrNot)
                bs.Append("OR NOT ");

            if (_elements.Count > 0)
            {
                bs.Append("( ");

                while (_elements.Count > 0)
                {
                    IQueryElement element = _elements.Dequeue();
                    element.Parse(database,bs, parametersList, this, alias);
                }

                bs.Append(") ");
            }
            else
            {
                if (RawExpression.IsEmpty())
                {
                    if (alias.IsNotEmpty() && !Field.Contains("."))
                    {
                        bs.Append(alias);
                        bs.Append(".");
                    }

                    bs.Append(database.GetName(Field));
                    bs.Append(" ");
                    bs.Append(Operator);
                    bs.Append(" ? ");
                }
                else
                {
                    bs.Append(RawExpression);
                    bs.Append(" ");
                }
            }
        }


        public QueryWhere Where(string field, object value)
        {
            return Where(field, "=", value);
        }

        public QueryWhere Where(string field, string operation, object value)
        {
            _addWhere(eBooleanConnector.None, field, operation, value);

            return this;
        }

        public QueryWhere WhereRaw(string expression, params object[] values)
        {
            _addWhereRaw(eBooleanConnector.None, expression, values);

            return this;
        }

        public QueryWhere Where(Func<QueryWhere, QueryWhere> callback)
        {
            _addWhereSub(eBooleanConnector.None, callback);

            return this;
        }

        public QueryWhere And(string field, object value)
        {
            return And(field, "=", value);
        }

        public QueryWhere And(string field, string operation, object value)
        {
            _addWhere(eBooleanConnector.And, field, operation, value);

            return this;
        }

        public QueryWhere AndRaw(string expression, params object[] values)
        {
            _addWhereRaw(eBooleanConnector.And, expression, values);

            return this;
        }

        public QueryWhere And(Func<QueryWhere, QueryWhere> callback)
        {
            _addWhereSub(eBooleanConnector.And, callback);

            return this;
        }

        public QueryWhere Or(string field, object value)
        {
            return Or(field, "=", value);
        }

        public QueryWhere Or(string field, string operation, object value)
        {
            _addWhere(eBooleanConnector.Or, field, operation, value);

            return this;
        }

        public QueryWhere OrRaw(string expression, params object[] values)
        {
            _addWhereRaw(eBooleanConnector.Or, expression, values);

            return this;
        }

        public QueryWhere Or(Func<QueryWhere, QueryWhere> callback)
        {
            _addWhereSub(eBooleanConnector.Or, callback);

            return this;
        }

        public QueryWhere AndNot(string field, object value)
        {
            return AndNot(field, "=", value);
        }

        public QueryWhere AndNot(string field, string operation, object value)
        {
            _addWhere(eBooleanConnector.AndNot, field, operation, value);

            return this;
        }

        public QueryWhere AndNotRaw(string expression, params object[] values)
        {
            _addWhereRaw(eBooleanConnector.Or, expression, values);

            return this;
        }

        public QueryWhere AndNot(Func<QueryWhere, QueryWhere> callback)
        {
            _addWhereSub(eBooleanConnector.AndNot, callback);

            return this;
        }

        public QueryWhere OrNot(string field, object value)
        {
            return OrNot(field, "=", value);
        }

        public QueryWhere OrNot(string field, string operation, object value)
        {
            _addWhere(eBooleanConnector.OrNot, field, operation, value);

            return this;
        }

        public QueryWhere OrNotRaw(string expression, params object[] values)
        {
            _addWhereRaw(eBooleanConnector.OrNot, expression, values);

            return this;
        }

        public QueryWhere OrNot(Func<QueryWhere, QueryWhere> callback)
        {
            _addWhereSub(eBooleanConnector.OrNot, callback);

            return this;
        }


        private void _addWhere(eBooleanConnector connector, string field, string operation, object value)
        {
            if (connector == eBooleanConnector.None && _elements.Count > 0)
            {
                throw new InvalidOperationException(
                    "Where kann nicht mehrfach in einer Ebene verwendet werden. Nutzen Sie stattdessen And, Or etc.");
            }

            if (connector != eBooleanConnector.None && (_elements.Count < 1 || !(_elements.PeekLast() is QueryWhere)))
                throw new InvalidOperationException($"{connector} kann nur nach einem Where verwendet werden.");

            _elements.Enqueue(new QueryWhere(field, new [] {value}, operation, connector));
        }

        private void _addWhereRaw(eBooleanConnector connector, string expression, params object[] values)
        {
            if (connector == eBooleanConnector.None && _elements.Count > 0)
            {
                throw new InvalidOperationException(
                    "Where kann nicht mehrfach in einer Ebene verwendet werden. Nutzen Sie stattdessen And, Or u.ä..");
            }

            if (connector != eBooleanConnector.None && (_elements.Count < 1 || !(_elements.PeekLast() is QueryWhere)))
                throw new InvalidOperationException($"{connector} kann nur nach einem Where verwendet werden.");

            _elements.Enqueue(new QueryWhere(expression, values, connector));
        }

        private void _addWhereSub(eBooleanConnector connector, Func<QueryWhere, QueryWhere> callback)
        {
            if (connector == eBooleanConnector.None && _elements.Count > 0)
            {
                throw new InvalidOperationException(
                    "Where kann nicht mehrfach in einer Ebene verwendet werden. Nutzen Sie stattdessen And, Or u.ä..");
            }

            if (connector != eBooleanConnector.None && (_elements.Count < 1 || !(_elements.PeekLast() is QueryWhere)))
                throw new InvalidOperationException($"{connector} kann nur nach einem Where verwendet werden.");

            QueryWhere where = new QueryWhere(connector);
            callback(where);

            _elements.Enqueue(where);
        }
    }

}

/// <summary>
/// Erweiterte Version von Queue
/// </summary>
/// <typeparam name="T"></typeparam>
public class QueueEx<T> : Queue<T>
{
    private T? last;

    /// <summary>
    /// Fügt am Ende der System.Collections.Generic.Queue`1 ein Objekt hinzu.
    /// </summary>
    /// <param name="item">
    ///  Das Objekt, das System.Collections.Generic.Queue`1 hinzugefügt werden soll. Der Wert kann für Verweistypen null sein.
    /// </param>
    public new void Enqueue(T item)
    {
        last = item;
        base.Enqueue(item);
    }

    /// <summary>
    /// Das zuletzt hinzugefügte Element
    /// </summary>
    /// <returns></returns>
    public T? PeekLast()
    {
        return last;
    }
}