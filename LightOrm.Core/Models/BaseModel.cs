using System;
using LightOrm.Core.Attributes;

namespace LightOrm.Core.Models
{
    public interface IModel
    {
        string GetTableName();
    }

    public abstract class BaseModel<T, TId> : IModel where T : BaseModel<T, TId>, new()
    {
        public abstract string TableName { get; }

        [Column("Id", isPrimaryKey: true, autoIncrement: true)]
        public TId Id { get; set; }

        [Column("CreatedAt")]
        public DateTime CreatedAt { get; set; }

        [Column("UpdatedAt")]
        public DateTime UpdatedAt { get; set; }

        public string GetTableName() => TableName;
    }
}
