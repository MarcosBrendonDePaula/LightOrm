using System;
using System.Threading.Tasks;
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

        // ---------- Hooks ----------
        // Override para validar, normalizar, gerar valores derivados, etc.
        // BeforeSave roda antes do INSERT/UPDATE; AfterSave depois do commit.
        // BeforeDelete antes do DELETE; AfterDelete depois.
        // AfterLoad é chamado após hidratar a entidade do banco.
        // Versões síncronas existem para conveniência; o repositório aguarda
        // ambas (a Async é chamada após a síncrona).
        protected internal virtual void OnBeforeSave(bool isInsert) { }
        protected internal virtual Task OnBeforeSaveAsync(bool isInsert) => Task.CompletedTask;
        protected internal virtual void OnAfterSave(bool isInsert) { }
        protected internal virtual Task OnAfterSaveAsync(bool isInsert) => Task.CompletedTask;
        protected internal virtual void OnBeforeDelete() { }
        protected internal virtual Task OnBeforeDeleteAsync() => Task.CompletedTask;
        protected internal virtual void OnAfterDelete() { }
        protected internal virtual Task OnAfterDeleteAsync() => Task.CompletedTask;
        protected internal virtual void OnAfterLoad() { }
        protected internal virtual Task OnAfterLoadAsync() => Task.CompletedTask;
    }
}
