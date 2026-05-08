using System.Collections.Generic;
using System.Threading.Tasks;

namespace LightOrm.Core.Models
{
    public interface IRepository<T, TId> where T : BaseModel<T, TId>, new()
    {
        Task EnsureSchemaAsync();
        Task<T> SaveAsync(T entity);
        Task<IReadOnlyList<T>> SaveManyAsync(IEnumerable<T> entities);

        // Insere se o id não existe; atualiza se existe. Útil quando o caller
        // gera o id (string/Guid) e precisa de "create-or-replace" idempotente.
        Task<T> UpsertAsync(T entity);

        // Builder fluente: filtros, ordenação, paginação, count, any.
        IQuery<T, TId> Query();
        Task<T> FindByIdAsync(TId id, bool includeRelated = false);
        Task<List<T>> FindAllAsync(bool includeRelated = false);
        Task DeleteAsync(T entity);
    }
}
