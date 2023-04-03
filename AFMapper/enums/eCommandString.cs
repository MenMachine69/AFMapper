
namespace AFMapper;

/// <summary>
/// Symbols for SQL commands used in ITranslator
/// </summary>
public enum eCommandString
{
    /// <summary>
    /// 
    /// </summary>
    DropTable,
    /// <summary>
    /// 
    /// </summary>
    DropIndex,
    /// <summary>
    /// 
    /// </summary>
    DropView,
    /// <summary>
    /// 
    /// </summary>
    DropProcedure,
    /// <summary>
    /// 
    /// </summary>
    DropTrigger,
    /// <summary>
    /// 
    /// </summary>
    DropField,
    /// <summary>
    /// 
    /// </summary>
    ExistTable,
    /// <summary>
    /// 
    /// </summary>
    ExistIndex,
    /// <summary>
    /// 
    /// </summary>
    ExistView,
    /// <summary>
    /// 
    /// </summary>
    ExistProcedure,
    /// <summary>
    /// 
    /// </summary>
    ExistTrigger,
    /// <summary>
    /// 
    /// </summary>
    CreateTable,
    /// <summary>
    /// 
    /// </summary>
    CreateIndex,
    /// <summary>
    /// 
    /// </summary>
    CreateView,
    /// <summary>
    /// 
    /// </summary>
    CreateProcedure,
    /// <summary>
    /// 
    /// </summary>
    CreateTrigger,
    /// <summary>
    /// 
    /// </summary>
    CreateField,
    /// <summary>
    /// 
    /// </summary>
    CreateKeyField,
    /// <summary>
    /// 
    /// </summary>
    TriggerBeforeInsert,
    /// <summary>
    /// 
    /// </summary>
    TriggerBeforeUpdate,
    /// <summary>
    /// 
    /// </summary>
    TriggerBeforeInsertFunc,
    /// <summary>
    /// 
    /// </summary>
    TriggerBeforeUpdateFunc,
    /// <summary>
    /// 
    /// </summary>
    BeforeAlterSchema,
    /// <summary>
    /// 
    /// </summary>
    EnableTrigger,
    /// <summary>
    /// 
    /// </summary>
    DisableTrigger,
    /// <summary>
    /// 
    /// </summary>
    GetServerTime,
    /// <summary>
    /// 
    /// </summary>
    FieldDefType,
    /// <summary>
    /// 
    /// </summary>
    FieldDefBool,
    /// <summary>
    /// 
    /// </summary>
    FieldDefImage,
    /// <summary>
    /// 
    /// </summary>
    FieldDefGuid,
    /// <summary>
    /// 
    /// </summary>
    FieldDefDateTime,
    /// <summary>
    /// 
    /// </summary>
    FieldDefBinary,
    /// <summary>
    /// 
    /// </summary>
    FieldDefInt,
    /// <summary>
    /// 
    /// </summary>
    FieldDefDecimal,
    /// <summary>
    /// 
    /// </summary>
    FieldDefDouble,
    /// <summary>
    /// 
    /// </summary>
    /// <summary>
    /// 
    /// </summary>
    FieldDefByte,
    /// <summary>
    /// 
    /// </summary>
    FieldDefShort,
    /// <summary>
    /// 
    /// </summary>
    FieldDefInt16,
    /// <summary>
    /// 
    /// </summary>
    FieldDefInt32,
    /// <summary>
    /// 
    /// </summary>
    FieldDefInt64,
    /// <summary>
    /// 
    /// </summary>
    FieldDefFloat,
    /// <summary>
    /// 
    /// </summary>
    FieldDefLong,
    /// <summary>
    /// 
    /// </summary>
    FieldDefObject,
    /// <summary>
    /// 
    /// </summary>
    FieldDefString,
    /// <summary>
    /// 
    /// </summary>
    FieldDefMemo,
    /// <summary>
    /// 
    /// </summary>
    GetSchema,
    /// <summary>
    /// 
    /// </summary>
    KeyWordFirst,
    /// <summary>
    /// 
    /// </summary>
    EventBeforeInsert,
    /// <summary>
    /// 
    /// </summary>
    EventBeforeUpdate,
    /// <summary>
    /// 
    /// </summary>
    EventBeforeDelete,
    /// <summary>
    /// 
    /// </summary>
    EventAfterInsert,
    /// <summary>
    /// 
    /// </summary>
    EventAfterUpdate,
    /// <summary>
    /// 
    /// </summary>
    EventAfterDelete,
    /// <summary>
    /// 
    /// </summary>
    LoadValue,
    /// <summary>
    /// 
    /// </summary>
    AlterFieldLength,
    /// <summary>
    /// 
    /// </summary>
    SelectCount,
    /// <summary>
    /// 
    /// </summary>
    SelectSum,
    /// <summary>
    /// 
    /// </summary>
    Select,
    /// <summary>
    /// 
    /// </summary>
    SelectTop,
    /// <summary>
    /// 
    /// </summary>
    Top,
    /// <summary>
    /// 
    /// </summary>
    Load,
    /// <summary>
    /// 
    /// </summary>
    Delete,
    /// <summary>
    /// 
    /// </summary>
    Update,
    /// <summary>
    /// 
    /// </summary>
    Insert,
    /// <summary>
    /// 
    /// </summary>
    ExecProcedure,
    /// <summary>
    /// 
    /// </summary>
    Exist,
    /// <summary>
    /// 
    /// </summary>
    AfterAlterSchema,
    /// <summary>
    /// 
    /// </summary>
    ExistConstraint,
    /// <summary>
    /// 
    /// </summary>
    DropConstraint,
    /// <summary>
    /// 
    /// </summary>
    CreateConstraint
}