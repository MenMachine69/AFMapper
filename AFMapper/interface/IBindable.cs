namespace AFMapper;

/// <summary>
/// Interface that a type must implement if it is to be available via 
/// TypeDescription if it is to be available. This identifies the type as a type, 
/// that supports CR3 databinding.
/// </summary>
public interface IBindable
{
    /// <summary>
    /// Copy all Content from a object to the current object
    /// </summary>
    /// <param name="source">source object</param>
    /// <param name="keyFields">copy keyFields (PrimaryKey, CreateAt, LastChanged, Archived) too</param>
    void CopyFrom(IBindable source, bool keyFields);
}

