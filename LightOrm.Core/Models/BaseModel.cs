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

        // Preenchido por DeleteAsync quando [SoftDelete] está ativo na classe.
        // Sem [SoftDelete], a propriedade existe mas NÃO vira coluna (filtrada
        // em BuildSchemaStatements).
        public DateTime? DeletedAt { get; set; }

        public string GetTableName() => TableName;

        // ---------- Hooks de ciclo de vida ----------
        // Existem dois tipos:
        //  - "OnBefore*"/"OnAfter*": notificações. Use para mutar a entidade,
        //    log, side-effects. Jogar exceção aborta a operação.
        //  - "Can*Async": cancelamento sem exceção. Retorne false para abortar
        //    silenciosamente. O repositório retorna a entidade sem mudanças
        //    visíveis (no caso de save) ou pula o delete.
        //
        // Granularidade:
        //  - Save: OnBeforeSave + (OnBeforeCreate ou OnBeforeUpdate); valida;
        //    operação; OnAfterSave + (OnAfterCreate ou OnAfterUpdate).
        //  - Delete: OnBeforeDelete; operação; OnAfterDelete.
        //  - Restore: OnBeforeRestore; operação; OnAfterRestore.
        //  - Validate: OnBeforeValidate; ModelValidator; OnAfterValidate.
        //  - Load: OnAfterLoad após hidratação.
        //
        // Versões síncronas e async existem para todos os hooks; o repo
        // aguarda a Async após executar a síncrona.

        protected internal virtual void OnBeforeSave(bool isInsert) { }
        protected internal virtual Task OnBeforeSaveAsync(bool isInsert) => Task.CompletedTask;
        protected internal virtual void OnAfterSave(bool isInsert) { }
        protected internal virtual Task OnAfterSaveAsync(bool isInsert) => Task.CompletedTask;

        protected internal virtual void OnBeforeCreate() { }
        protected internal virtual Task OnBeforeCreateAsync() => Task.CompletedTask;
        protected internal virtual void OnAfterCreate() { }
        protected internal virtual Task OnAfterCreateAsync() => Task.CompletedTask;

        protected internal virtual void OnBeforeUpdate() { }
        protected internal virtual Task OnBeforeUpdateAsync() => Task.CompletedTask;
        protected internal virtual void OnAfterUpdate() { }
        protected internal virtual Task OnAfterUpdateAsync() => Task.CompletedTask;

        protected internal virtual void OnBeforeValidate() { }
        protected internal virtual Task OnBeforeValidateAsync() => Task.CompletedTask;
        protected internal virtual void OnAfterValidate() { }
        protected internal virtual Task OnAfterValidateAsync() => Task.CompletedTask;

        protected internal virtual void OnBeforeDelete() { }
        protected internal virtual Task OnBeforeDeleteAsync() => Task.CompletedTask;
        protected internal virtual void OnAfterDelete() { }
        protected internal virtual Task OnAfterDeleteAsync() => Task.CompletedTask;

        protected internal virtual void OnBeforeRestore() { }
        protected internal virtual Task OnBeforeRestoreAsync() => Task.CompletedTask;
        protected internal virtual void OnAfterRestore() { }
        protected internal virtual Task OnAfterRestoreAsync() => Task.CompletedTask;

        protected internal virtual void OnAfterLoad() { }
        protected internal virtual Task OnAfterLoadAsync() => Task.CompletedTask;

        // ---------- Cancelamento ----------
        // Retorne false para abortar a operação silenciosamente. Útil para
        // implementar middleware que "engole" certas chamadas (ex.: usuário
        // sem permissão, soft-mode, etc.) sem lançar exceção.
        protected internal virtual Task<bool> CanSaveAsync(bool isInsert) => Task.FromResult(true);
        protected internal virtual Task<bool> CanDeleteAsync() => Task.FromResult(true);
    }
}
