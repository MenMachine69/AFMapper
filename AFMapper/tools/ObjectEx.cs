
using System.IO.Compression;
using System.Numerics;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml;
using System.Xml.Serialization;

namespace AFMapper;

/// <summary>
/// Extension methods for the System.Object class
/// </summary>
public static class ObjectEx
{
    /// <summary>
    /// Determines whether an object can be accessed via a specific Get interface.
    /// </summary>
    /// <param name="obj">the object</param>.
    /// <param name="getter">Name of the property</param>.
    /// <returns>true if the object has a corresponding interface, otherwise false</returns>
    public static bool HasGet(this object obj, string getter)
    {
        return GetGetter(obj, getter) != null;
    }

    /// <summary>
    /// Determines whether an object has a specific Get interface and returns the appropriate PropertyInfo object.
    /// </summary>
    /// <param name="obj">the object</param>.
    /// <param name="getter">name of the property</param>.
    /// <returns>PropertyInfo of the getter if the object has a corresponding interface, otherwise null</returns>
    public static PropertyInfo? GetGetter(this object obj, string getter)
    {
        PropertyInfo? ret = obj.GetType().GetProperty(getter,
            BindingFlags.GetProperty | BindingFlags.GetField | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy |
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

        return ret == null || !ret.CanRead ? null : ret;
    }

    /// <summary>
    /// Returns the type of a specific Get interface of an object.
    /// </summary>
    /// <param name="obj">the object</param>.
    /// <param name="getter">name of the property</param>.
    /// <returns>Type of data supplied by the get</returns>
    public static Type? GetTypeOfGet(this object obj, string getter)
    {
        return GetGetter(obj, getter)?.PropertyType;
    }

    /// <summary>
    /// Returns the type of a specific set interface of an object.
    /// </summary>
    /// <param name="obj">the object</param>.
    /// <param name="setter">name of the property</param>.
    /// <returns>Type of data expected from the set</returns>
    public static Type? GetTypeOfSet(this object obj, string setter)
    {
        return GetSetter(obj, setter)?.PropertyType;
    }

    /// <summary>
    /// Determines whether an object can be accessed via a specific set interface.
    /// </summary>
    /// <param name="obj">the object</param>.
    /// <param name="setter">name of the property</param>.
    /// <returns>true if the object has a corresponding interface, otherwise false</returns>
    public static bool HasSet(this object obj, string setter)
    {
        return GetSetter(obj, setter) != null;
    }

    /// <summary>
    /// Determines whether an object has a specific Set interface and returns the appropriate PropertyInfo object.
    /// </summary>
    /// <param name="obj">the object</param>.
    /// <param name="setter">name of the property</param>.
    /// <returns>PropertyInfo of the setter if the object has a corresponding interface, otherwise null</returns>
    public static PropertyInfo? GetSetter(this object obj, string setter)
    {
        PropertyInfo? ret = obj.GetType().GetProperty(setter,
            BindingFlags.SetProperty | BindingFlags.SetField | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy |
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

        return ret == null || !ret.CanWrite ? null : ret;
    }

    /// <summary>
    /// Determines whether an object has a specific method.
    /// </summary>
    /// <param name="obj">Name of the object</param>.
    /// <param name="method">Name of the method (case sensitive!)</param>.
    /// <returns>true if the object has a corresponding method, otherwise false</returns>
    public static bool HasMethod(this object obj, string method)
    {
        return obj.GetType().GetMethod(method,
            BindingFlags.NonPublic | BindingFlags.FlattenHierarchy | BindingFlags.Public | BindingFlags.Instance |
            BindingFlags.IgnoreCase) == null ^ true;
    }

    /// <summary>
    /// calls a method in an object by the name of the method.
    /// If the method does not exist, a method called NoMethod is searched for in the object, and if it is found 
    /// is found, it is also called.
    /// </summary>
    /// <param name="obj">Object in which the method is called</param>.
    /// <param name="method">Name of the method to be called</param>.
    /// <param name="args">Arguments to be passed with the method</param>.
    /// <returns>Returns the method, otherwise null</returns>
    public static object? InvokeMethod(this object obj, string method, params object[] args)
    {
        return obj.GetType().InvokeMember(method,
            BindingFlags.InvokeMethod | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy |
            BindingFlags.Instance | BindingFlags.IgnoreCase, null, obj, args);
    }

    /// <summary>
    /// Fetches the value of an property by accessing the get code by name 
    /// </summary>
    /// <param name="obj">object in which contains the property</param>.
    /// <param name="getter">name of the property</param>.
    /// <returns>value of the property</returns>
    public static object? InvokeGet(this object obj, string getter)
    {
        return GetGetter(obj, getter)?.GetValue(obj, null);
    }

    /// <summary>
    /// Assigns a value to a property by accessing the set code by name. 
    /// </summary>
    /// <param name="obj">Object containing the property</param>.
    /// <param name="setter">Name of the property</param>.
    /// <param name="value">Value to be assigned</param>
    public static void InvokeSet(this object obj, string setter, object value)
    {
        GetSetter(obj, setter)?.SetValue(obj, value, null);
    }
    
    /// <summary>
    /// Serialise object as json into a stream
    /// </summary>
    /// <param name="obj">Object to be serialised</param>.
    /// <param name="stream">stream to serialise into</param>
    public static void Serialize(this object obj, Stream stream)
    {
        JsonSerializer.Serialize(stream, obj,
            new JsonSerializerOptions
            {
                WriteIndented = false,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });
    }

    /// <summary>
    /// Deserialise object from a json stream
    /// </summary>
    /// <typeparam name="T">Type of object</typeparam>.
    /// <param name="stream">stream containing the serialised object</param>.
    /// <returns>the deserialised object or a new object of T</returns>
    public static T Deserialize<T>(Stream stream) where T : new()
    {
        T ret = new();

        var read = JsonSerializer.Deserialize(stream, typeof(T));

        if (read is T) ret = (T)read;

        return ret;
    }

    /// <summary>
    /// Serialise object as json into a file
    /// </summary>
    /// <param name="obj">object to be serialised</param>.
    /// <param name="file">file to write the object to</param>.
    public static void Serialize(this object obj, FileInfo file)
    {
        if (file.Directory == null)
            throw new NullReferenceException("File does not contain a directory information.");

        if (!file.Directory.Exists)
            file.Directory.Create();

        using FileStream stream = file.Create();
        obj.Serialize(stream);
        stream.Flush();
    }

    /// <summary>
    /// Deserialise object from a json file
    /// </summary>
    /// <typeparam name="T">Type of object</typeparam>.
    /// <param name="file">File that contains the serialised object</param>.
    /// <returns>deserialised object</returns>
    public static T Deserialize<T>(this FileInfo file) where T : new()
    {
        using FileStream stream = file.OpenRead();
        return Deserialize<T>(stream);
    }

    /// <summary>
    /// Serialise object as xml into a file
    /// </summary>
    /// <param name="obj">object to be serialised</param>.
    /// <param name="file">file to write the object to</param>.
    public static void SerializeXml(this object obj, FileInfo file)
    {
        if (file.Directory == null)
            throw new NullReferenceException("File does not contain a directory information.");

        if (!file.Directory.Exists)
            file.Directory.Create();

        using StreamWriter stream = new(file.FullName, false, Encoding.UTF8);
        XmlSerializer serializer = new(obj.GetType());
        serializer.Serialize(stream, obj);
        stream.Flush();
    }

    /// <summary>
    /// Objekt in einen XML-String serialisieren
    /// </summary>
    /// <param name="obj">zu serialisierendes Objekt</param>
    /// <returns>XML String mit den serialsierten Daten</returns>
    public static string SerializeXml(this object obj)
    {
        StringBuilder output = new();

        using (XmlWriter stream = XmlWriter.Create(output))
        {
            XmlSerializer serializer = new(obj.GetType());
            serializer.Serialize(stream, obj);
            stream.Flush();
        }

        return output.ToString();
    }

    /// <summary>
    /// Deserialise object from a json file
    /// </summary>
    /// <typeparam name="T">Type of object</typeparam>.
    /// <param name="file">File that contains the serialised object</param>.
    /// <returns>deserialised object</returns>
    public static T? DeserializeXml<T>(this FileInfo file) where T : new()
    {
        T? ret = default;

        if (!file.Exists)
            throw new FileNotFoundException($"File {file.FullName} not found.");

        using StreamReader stream = new(file.FullName, Encoding.UTF8);
        XmlSerializer bin = new(typeof(T));
        var read = bin.Deserialize(stream);

        if (read is T) ret = (T)read;

        return ret;
    }

    /// <summary>
    /// Objekt in einen JSON-String serialisieren
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    public static string SerializeJson(this object obj)
    {
        return Encoding.UTF8.GetString(JsonSerializer.SerializeToUtf8Bytes(obj,
            new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            }));
    }

    /// <summary>
    /// Objekt aus JSON-String deserialisieren
    /// </summary>
    /// <typeparam name="T">Type des Objekts</typeparam>
    /// <param name="data">serialsiertes Objekt</param>
    /// <returns>deserialsiertes Objekt oder null</returns>
    public static T? DeserializeJson<T>(this string data) where T : new()
    {
        T? ret = default;

        var read = JsonSerializer.Deserialize(Encoding.UTF8.GetBytes(data), typeof(T));

        if (read is T) ret = (T)read;

        return ret;
    }

    /// <summary>
    /// Objekt via JSON in ein Byte-Array serialsieren (UTF8 Bytes)
    /// </summary>
    /// <param name="obj">das zu serialisierende Objekt</param>
    /// <returns>ByteArray mit den serialsierten Daten</returns>
    public static byte[] Serialize(this object obj)
    {
        return JsonSerializer.SerializeToUtf8Bytes(obj,
            new JsonSerializerOptions
            {
                WriteIndented = false,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });
    }


    /// <summary>
    /// Deserialise an object byte[] that contains the object as serialised json
    /// </summary>
    /// <param name="array">serialised object</param>.
    /// <typeparam name="T">type of object</typeparam>
    /// <returns>the deserialised object or null</returns>
    public static T? Deserialize<T>(this byte[] array) where T : new()
    {
        return (T?)Deserialize(typeof(T), array);
    }

    /// <summary>
    /// Deserialise an object byte[] that contains the object as serialised json
    /// </summary>
    /// <param name="array">serialised object</param>.
    /// <param name="type">type of object</param>.
    /// <returns>the deserialised object or null</returns>
    public static object? Deserialize(Type type, byte[] array)
    {
        return JsonSerializer.Deserialize(array, type);
    }

    /// <summary>
    /// Compress a byte[] via ZIP
    /// </summary>
    /// <param name="array">data to be compressed</param>.
    /// <returns>byte array with the compressed data</returns>.
    public static byte[] Compress(this byte[] array)
    {
        using var memoryStream = new MemoryStream();
        using var gZipStream = new GZipStream(memoryStream, CompressionMode.Compress);
        gZipStream.Write(array, 0, array.Length);

        return memoryStream.ToArray();
    }

    /// <summary>
    /// decompress via ZIP compressed byte[]
    /// </summary>
    /// <param name="array">compressed data</param>.
    /// <returns> uncompressed data</returns>
    public static byte[] Decompress(this byte[] array)
    {
        using var memoryStream = new MemoryStream(array);
        using var gZipStream = new GZipStream(memoryStream, CompressionMode.Decompress);
        using var memoryStreamOutput = new MemoryStream();
        gZipStream.CopyTo(memoryStreamOutput);
        return memoryStreamOutput.ToArray();
    }

    /// <summary>
    /// Call a generic method
    /// 
    /// conn.InvokeGeneric("Check", new Type[] {typeof(User)}, null)
    /// conn.InvokeGeneric("Check", new Type[] {typeof(User)}, "Heiko", "Müller")
    /// </summary>
    /// <param name="obj">Object for which the generic method should be called</param>.
    /// <param name="name">Name of the method</param>.
    /// <param name="parameters">Parameters to pass to the method</param>.
    /// <param name="genTypes">generic types</param>
    public static object? InvokeGeneric(this object obj, string name, Type[] genTypes, params object[] parameters)
    {
        var paraTypes = parameters.AsEnumerable().Select(o => o.GetType()).ToArray();

        var mi = obj.GetType().GetMethod(name, paraTypes);

        if (mi == null)
            mi = obj.GetType().GetExtensionMethod(name, true);

        if (mi == null)
            throw new Exception($"InvokeGeneric: Generic method {name} not found.");

        var mref = mi.MakeGenericMethod(genTypes);

        return mref.Invoke(obj, parameters);

    }

    /// <summary>
    /// Call a generic method
    /// 
    /// conn.InvokeGeneric("Check", typeof(User))
    /// </summary>
    /// <param name="obj">Object for which to call the generic method</param>.
    /// <param name="name">Name of the method</param>.
    /// <param name="genericType"></param>
    public static object? InvokeGeneric(this object obj, string name, Type genericType)
    {
        var mi = obj.GetType().GetMethods()
            .FirstOrDefault(method => method.Name == name && !method.GetParameters().Any());

        if (mi == null)
            throw new Exception($"InvokeGeneric: Generic method {name} not found.");

        var mref = mi.MakeGenericMethod(genericType);

        return mref.Invoke(obj, null);

    }

    /// <summary>
    /// Converts a value to an Int64 value
    /// 
    /// If the value passed is a BigInteger, and this exceeds the value 
    /// of Int64.MaxValue, an OverflowException is thrown.
    /// </summary>
    /// <param name="obj">Value to be converted</param>.
    /// <returns>Int64 Value</returns>
    public static long ToInt64(this object obj)
    {
        return obj switch
        {
            long l => l,
            BigInteger integer when integer > long.MaxValue => throw new OverflowException(),
            BigInteger integer => (long)(integer % long.MaxValue),
            _ => Convert.ToInt64(obj)
        };
    }
}

