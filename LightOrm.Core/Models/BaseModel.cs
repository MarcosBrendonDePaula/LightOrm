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
        // Default: lê [Table("nome")] da classe. Override para forçar um nome
        // sem depender do atributo. Lança se nenhum dos dois existir.
        public virtual string TableName
        {
            get
            {
                var attr = LightOrm.Core.Utilities.TypeMetadataCache.GetTableAttribute(typeof(T));
                if (attr != null) return attr.Name;
                throw new InvalidOperationException(
                    $"Tipo {typeof(T).Name} precisa de [Table(\"nome\")] na classe ou override de TableName.");
            }
        }

        [Column("Id", isPrimaryKey: true, autoIncrement: true)]
        public TId Id { get; set; }

        [Column("CreatedAt")]
        public DateTime CreatedAt { get; set; }

        [Column("UpdatedAt")]
        public DateTime UpdatedAt { get; set; }

        public string GetTableName() => TableName;
    }
}
