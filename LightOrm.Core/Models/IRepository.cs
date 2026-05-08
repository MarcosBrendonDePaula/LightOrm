using System.Collections.Generic;
using System.Threading.Tasks;

namespace LightOrm.Core.Models
{
    public interface IRepository<T, TId> where T : BaseModel<T, TId>, new()
    {
        Task EnsureSchemaAsync();
        Task<T> SaveAsync(T entity);
        Task<T> FindByIdAsync(TId id, bool includeRelated = false);
        Task<List<T>> FindAllAsync(bool includeRelated = false);
        Task DeleteAsync(T entity);
    }
}
