
namespace AFMapper;

/// <summary>
/// Interface for classes that are reflected as tables in a database.
///
/// If BaseTableData is used it is not neccesary to implement this interface
/// in your own classes because ist allready impelmented in BaseTableData. 
/// </summary>
public interface ITable : IDataObject
{
    /// <summary>
    /// Method that will be raised before a model will be stored to database
    /// </summary>
    void BeforeSave() { }

}