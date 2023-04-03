using System.IO.Compression;
using System.Numerics;
using System.Text.Json;

namespace AFMapper;

/// <summary>
/// Baseclass for Translator classes which encapsulate 
/// shared properties and methods for all these classes.
/// </summary>
public abstract class BaseTranslator
{
    private readonly Dictionary<eCommandString, string> _commands = new();

    /// <summary>
    /// Constructor
    /// </summary>
    protected BaseTranslator()
    {
        _commands.Add(eCommandString.EventAfterDelete, "AFTER DELETE");
        _commands.Add(eCommandString.EventAfterInsert, "AFTER INSERT");
        _commands.Add(eCommandString.EventAfterUpdate, "AFTER UPDATE");
        _commands.Add(eCommandString.EventBeforeDelete, "BEFORE DELETE");
        _commands.Add(eCommandString.EventBeforeInsert, "BEFORE INSERT");
        _commands.Add(eCommandString.EventBeforeUpdate, "BEFORE UPDATE");
        _commands.Add(eCommandString.CreateView, "CREATE VIEW #NAME# (#FIELDS#) AS #QUERY#");
        _commands.Add(eCommandString.DropIndex, "DROP INDEX #NAME#");
        _commands.Add(eCommandString.DropProcedure, "DROP PROCEDURE #NAME#");
        _commands.Add(eCommandString.DropTable, "DROP TABLE #NAME#");
        _commands.Add(eCommandString.DropTrigger, "DROP TRIGGER #NAME#");
        _commands.Add(eCommandString.DropView, "DROP VIEW #NAME#");
        _commands.Add(eCommandString.EnableTrigger, "ALTER TRIGGER #NAME# active");
        _commands.Add(eCommandString.DisableTrigger, "ALTER TRIGGER #NAME# inactive");
        _commands.Add(eCommandString.CreateField, "ALTER TABLE #TABLENAME# ADD #NAME# #FIELDOPTIONS#");
        _commands.Add(eCommandString.GetSchema, "SELECT * FROM #NAME#");
        _commands.Add(eCommandString.LoadValue, "SELECT #FIELDNAME# FROM #TABLENAME# WHERE #FIELDNAMEKEY# = ?");
        _commands.Add(eCommandString.SelectCount, "SELECT COUNT(#FIELDNAME#) FROM #TABLENAME#");
        _commands.Add(eCommandString.SelectSum, "SELECT SUM(#FIELDNAME#) FROM #TABLENAME#");
        _commands.Add(eCommandString.Select, "SELECT #FIELDNAMES# FROM #TABLENAME#");
        _commands.Add(eCommandString.Load, "SELECT #FIELDNAMES# FROM #TABLENAME# WHERE #FIELDNAMEKEY# = ?");
        _commands.Add(eCommandString.Delete, "DELETE FROM #TABLENAME# WHERE #FIELDNAMEKEY# = ?");
        _commands.Add(eCommandString.Update, "UPDATE #TABLENAME# set #PAIRS# WHERE #FIELDNAMEKEY# = @v0");
        _commands.Add(eCommandString.Insert, "INSERT INTO #TABLENAME# (#FIELDS#) VALUES (#VALUES#)");
        _commands.Add(eCommandString.ExecProcedure, "EXECUTE PROCEDURE #PROCEDURE#");
        _commands.Add(eCommandString.Exist, "SELECT #FIELDNAMEKEY# FROM #TABLENAME# WHERE #FIELDNAMEKEY# = ?");



        PlaceHolders = new()
        {
            { "TODAY", DateTime.Now.Date.ToShortDateString() },
            { "MONTH", DateTime.Now.Month },
            { "YEAR", DateTime.Now.Year },
            { "DAY", DateTime.Now.Day },
            { "YESTERDAY", DateTime.Now.Date.Subtract(new TimeSpan(1, 0, 0, 0)).ToShortDateString() },
            { "PASTMONTH", DateTime.Now.Month == 1 ? 12 : DateTime.Now.Month - 1 },
            { "PASTYEAR", DateTime.Now.Year - 1 },
            { "PASTDAY", DateTime.Now.Subtract(new TimeSpan(1, 0, 0, 0)).Day },
            { "TOMORROW", DateTime.Now.Date.AddDays(1).ToShortDateString() },
            { "FOLLOWMONTH", DateTime.Now.Month == 12 ? 1 : DateTime.Now.Month + 1 },
            { "FOLLOWYEAR", DateTime.Now.Year + 1 },
            { "FOLLOWDAY", DateTime.Now.AddDays(1).Day },
            { "HOUR", DateTime.Now.Hour },
            { "MINUTE", DateTime.Now.Minute },
            { "EMPTYGUID", "'00000000-0000-0000-0000-000000000000'" }
        };
    }


    /// <summary>
    /// Returns a globally valid string for the specific command.  
    /// Throws an exception if no command string is available for this command.
    /// </summary>
    /// <param name="command">needed Command string</param>
    /// <returns>command string</returns>
    public virtual string GetCommandString(eCommandString command)
    {
        if (_commands.ContainsKey(command))
            return _commands[command];

        throw new ArgumentException(string.Format(Strings.DBTRANS_NOCOMMANDDEFINED, command.ToString()),
            nameof(command));
    }

    /// <summary>
    /// A list (dictionary of name and value) of universal placeholders
    /// </summary>
    public Dictionary<string, object> PlaceHolders { get; }

    public List<StringParserSnippet> CustomFunctions { get; } = new();


    /// <summary>
    /// Trigger-Event in datenbankspezifisches Format übersetzen
    /// </summary>
    /// <param name="code"></param>
    /// <returns></returns>
    public virtual string GetTriggerEvent(eTriggerEvent code)
    {
        string ret = "";

        switch (code)
        {
            case eTriggerEvent.AfterDelete:
                ret = "AFTER DELETE";
                break;
            case eTriggerEvent.BeforeInsert:
                ret = "BEFORE INSERT";
                break;
            case eTriggerEvent.BeforeUpdate:
                ret = "BEFORE UPDATE";
                break;
            case eTriggerEvent.BeforeDelete:
                ret = "BEFORE DELETE";
                break;
            case eTriggerEvent.AfterInsert:
                ret = "AFTER INSERT";
                break;
            case eTriggerEvent.AfterUpdate:
                ret = "AFTER UPDATE";
                break;
        }

        return ret;
    }

    /// <summary>
    /// Translates the source text of the query into the database-specific format.
    /// 
    /// Example:
    /// select * from test where CRSubstring(CRSubstring(probe, 3, 4), 5, 5) = 'Test'
    /// becomes 
    /// select * from test where SUBSTRING(SUBSTRING(probe FROM 3 FOR 4) FROM 5 FOR 5) = 'Test' 
    /// 
    /// </summary>
    /// <param name="query">Abfrage</param>
    /// <returns>übersetzte Abfrage</returns>
    public string translate(ref string query)
    {
        StringFunctionParser parser = new StringFunctionParser();
        parser.SetSnippets(CustomFunctions);

        return parser.Parse(query);
    }


    /// <summary>
    /// Converts a value into the equivalent database value
    /// </summary>
    /// <param name="value">value to convert</param>
    /// <param name="valueType">Type of the value to be converted, if null is passed, the system attempts to determine the type from the value.</param>
    /// <param name="compress">compress data (ZIP)</param>
    /// <returns>converted value</returns>
    public virtual object ToDatabase(object value, Type? valueType, bool compress)
    {
        object target;

        valueType ??= value is Type ? typeof(Type) : value.GetType();

        var targetType = valueType;

        try
        {
            if (valueType.IsEnum)
                target = (int)value;
            else if (valueType == typeof(Guid))
            {
                if (value.Equals(Guid.Empty))
                    target = DBNull.Value;
                else
                {
                    targetType = typeof(Guid);
                    target = (Guid)value;
                }
            }
            else if (valueType == typeof(bool))
                target = value;
            else if (valueType == typeof(int))
                target = (int)value;
            else if (valueType == typeof(short))
                target = (short)value;
            else if (valueType == typeof(decimal))
                target = (decimal)value;
            else if (valueType == typeof(double))
                target = (double)value;
            else if (valueType == typeof(float))
                target = (float)value;
            else if (valueType == typeof(byte))
                target = (byte)value;
            else if (valueType == typeof(long))
                target = (long)value;
            else if (valueType == typeof(DateTime))
                target = (DateTime)value;
            else if (valueType == typeof(Type))
            {
                targetType = typeof(string);
                string? name = ((Type)value).FullName;
                target = name ?? "";
            }
            else if (valueType == typeof(string))
                target = ((string)value).IsEmpty() ? "" : value;
            else if (valueType == typeof(byte[]))
                target = value as byte[] ?? value.Serialize();
            else if (valueType == typeof(System.Drawing.Image) ||
                     valueType == typeof(System.Drawing.Bitmap)) // Image nach byte[]
            {
                targetType = typeof(byte[]);

                using (MemoryStream stream = new())
                {
                    try
                    {
                        if (valueType == typeof(System.Drawing.Image))
                            ((System.Drawing.Image)value).Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                        else
                            ((System.Drawing.Bitmap)value).Save(stream, System.Drawing.Imaging.ImageFormat.Png);

                        target = stream.ToArray();
                    }
                    catch (Exception ex)
                    {
                        throw new Exception(Strings.ERROR_WHILECONVERTIMAGETOBYTEARRAY, ex);
                    }
                }
            }
            else if (valueType.IsSerializable && valueType.IsValueType == false) // Object nach byte[]
            {
                targetType = typeof(byte[]);
                target = value.Serialize();
            }
            else
                target = value;
        }
        catch (Exception ex)
        {
            throw new Exception(Strings.ERROR_WHILETRANSLATINGTODB.DisplayWith(valueType, targetType), ex);
        }

        return target;
    }

    /// <summary>
    /// Converts a value into the equivalent database value
    /// </summary>
    /// <param name="value">value to convert</param>
    /// <returns>converted value</returns>
    public object ToDatabase(object value)
    {
        return ToDatabase(value, value.GetType(), true);
    }

    /// <summary>
    /// Converts a value into the equivalent database value
    /// </summary>
    /// <param name="value">value to convert</param>
    /// <param name="valuetype">Type of the value to be converted, if null is passed, the system attempts to determine the type from the value.</param>
    /// <returns>converted value</returns>
    public object ToDatabase(object value, Type valuetype)
    {
        return ToDatabase(value, valuetype, true);
    }

    /// <summary>
    /// Konvertiert einen Wert aus dem Datenbankformat in das Zielformat
    /// </summary>
    /// <param name="targettype">Zielformat</param>
    /// <param name="value">zu konvertierender Wert</param>
    /// <returns>konvertierter Wert oder default-Wert des Zielformats</returns>
    public virtual object? FromDatabase(object? value, Type targettype)
    {
        Type valtype = value == null ? typeof(DBNull) : value.GetType();
        object? tmp = null;

        try
        {
            if (targettype.IsEnum)
            {
                tmp = valtype == typeof(DBNull) || value == null
                    ? 0
                    : Enum.ToObject(targettype, Convert.ToInt32(value));
            }
            else if (targettype == typeof(string))
                tmp = valtype == typeof(DBNull) || value == null ? string.Empty : value.ToString();
            else if (targettype == typeof(int))
                tmp = valtype == typeof(DBNull) ? 0 : value is int intval ? intval : Convert.ToInt32(value);
            else if (targettype == typeof(decimal))
            {
                tmp = valtype == typeof(DBNull)
                    ? 0
                    : value as decimal? ?? Convert.ToDecimal(value);
            }
            else if (targettype == typeof(double))
                tmp = valtype == typeof(DBNull) ? 0 : value is double dblval ? dblval : Convert.ToDouble(value);
            else if (targettype == typeof(byte))
                tmp = valtype == typeof(DBNull) ? 0 : value is byte byteval ? byteval : Convert.ToByte(value);
            else if (targettype == typeof(short))
                tmp = valtype == typeof(DBNull) ? 0 : value is short shrtval ? shrtval : Convert.ToInt16(value);
            else if (targettype == typeof(float))
                tmp = valtype == typeof(DBNull) ? 0 : value is float fltval ? fltval : Convert.ToSingle(value);
            else if (targettype == typeof(long))
            {
                tmp = valtype == typeof(DBNull) ? 0 : 
                    value is long longval ? longval : value is BigInteger bigint
                        ? bigint.ToInt64() : Convert.ToInt64(value);
            }
            else if (targettype == typeof(DateTime))
            {
                tmp = valtype == typeof(DBNull)
                    ? DateTime.MinValue
                    : value as DateTime? ?? Convert.ToDateTime(value);
            }
            else if (targettype == typeof(byte[]))
                tmp = valtype == typeof(DBNull) || value == null ? new byte[] { } : (byte[])value;
            else if (targettype == typeof(Guid))
            {
                tmp = valtype == typeof(DBNull) || value == null
                    ? Guid.Empty
                    : value as Guid? ?? new Guid((byte[])value);
            }
            else if (targettype == typeof(bool))
            {
                if (valtype != typeof(DBNull) && value is string strval)
                    tmp = "JjYy1".Contains(strval);
                else
                    tmp = valtype != typeof(DBNull) && value != null && (bool)value;
            }
            else if (targettype == typeof(Type)) // Types sind als FullName gespeichert
            {
                tmp = valtype == typeof(DBNull) || value == null || ((string)value).IsEmpty()
                    ? typeof(Nullable)
                    : TypeEx.FindType((string)value);
            }
            else if (value is byte[] byteval && targettype != typeof(byte[]) &&
                     targettype != typeof(System.Drawing.Image) &&
                     targettype !=
                     typeof(System.Drawing.Bitmap)) // Byte-Arrays, bei denen der Zieltyp KEIN Byte-Array oder IMAGE ist,
                // werden als serialisierte Objekte betrachtet und
                // aus dem Byte-Array deserialisiert.

                tmp = valtype == typeof(DBNull) ? null : ObjectEx.Deserialize(targettype, byteval);
            else if ((targettype == typeof(System.Drawing.Image) || targettype == typeof(System.Drawing.Bitmap)) &&
                     value is byte[] bytval &&
                     valtype !=
                     typeof(DBNull)) // Bilder werden ebenfalls in einem Byte-Array gespeichert und aus diesem deserialisiert
            {
                if (bytval.Length > 0)
                {
                    using (MemoryStream stream = new(bytval))
                    {
                        try
                        {
                            tmp = System.Drawing.Image.FromStream(stream);
                        }
                        catch (Exception ex)
                        {
                            throw new Exception(Strings.ERROR_WHILECONVERTIMAGEFROMBYTEARRAY, ex);
                        }
                    }
                }
                else
                    tmp = null;
            }
        }
        catch (Exception ex)
        {
            throw new Exception(Strings.ERROR_WHILETRANSLATIONFROMDB, ex);
        }

        return tmp;
    }

    internal byte[] _toByteArray(object data)
    {
        using var to = new MemoryStream();
        using var gZipStream = new GZipStream(to, CompressionMode.Compress);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(data,
            new JsonSerializerOptions
            {
                WriteIndented = false,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });
        gZipStream.Write(bytes, 0, bytes.Length);
        gZipStream.Flush();
        return to.ToArray();
    }

    /// <summary>
    /// Objekt aus einem ByteArray deserialisieren...
    /// </summary>
    /// <param name="data">Byte-Array mit den Daten</param>
    /// <param name="toType">zu Typ deserialisieren</param>
    /// <returns>das deserialisierte Objekt (oder null)</returns>
    internal object? _fromByteArray(Type toType, byte[] data)
    {
        object? ret;

        using var from = new MemoryStream(data);
        using var to = new MemoryStream();
        using var gZipStream = new GZipStream(from, CompressionMode.Decompress);
        {
            gZipStream.CopyTo(to);
            ret = JsonSerializer.Deserialize(to, toType);
        }

        return ret;

        //return data.InvokeGeneric("Deserialize", new Type[] { toType }, data);
    }
}

