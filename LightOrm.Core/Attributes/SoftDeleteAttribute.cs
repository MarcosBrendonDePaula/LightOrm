using System;

namespace LightOrm.Core.Attributes
{
    /// <summary>
    /// Ativa soft delete na classe. DeleteAsync passa a fazer
    /// UPDATE deleted_at = now() em vez de DELETE. FindById/FindAll/Query
    /// filtram automaticamente registros com deleted_at não nulo, exceto
    /// pelos métodos *IncludingDeleted.
    ///
    /// O nome da coluna é configurável (default: "deleted_at"). A classe
    /// não precisa declarar a coluna manualmente — BaseModel descobre via
    /// SoftDeleteHelper e adiciona ao DDL automaticamente.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
    public class SoftDeleteAttribute : Attribute
    {
        public string ColumnName { get; }

        public SoftDeleteAttribute(string columnName = "deleted_at")
        {
            ColumnName = columnName;
        }
    }
}
