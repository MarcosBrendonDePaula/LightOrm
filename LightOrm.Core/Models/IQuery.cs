using System.Collections.Generic;
using System.Threading.Tasks;

namespace LightOrm.Core.Models
{
    /// <summary>
    /// Builder fluente comum a todos os backends. Métodos retornam o próprio
    /// builder para encadeamento; ToListAsync/FirstOrDefaultAsync/CountAsync/AnyAsync
    /// executam a query.
    ///
    /// Operadores válidos (case-insensitive): "=", "!=", "<>", "<", "<=", ">", ">=",
    /// "LIKE", "NOT LIKE". O filtro IS/IS NOT NULL é representado passando null no
    /// value do operador "=".
    /// </summary>
    public interface IQuery<T, TId> where T : BaseModel<T, TId>, new()
    {
        IQuery<T, TId> Where(string propertyName, string op, object value);
        IQuery<T, TId> Where(string propertyName, object value);
        IQuery<T, TId> WhereIn(string propertyName, IEnumerable<object> values);
        IQuery<T, TId> OrderBy(string propertyName);
        IQuery<T, TId> OrderByDescending(string propertyName);
        IQuery<T, TId> Take(int limit);
        IQuery<T, TId> Skip(int offset);

        Task<List<T>> ToListAsync();
        Task<T> FirstOrDefaultAsync();
        Task<int> CountAsync();
        Task<bool> AnyAsync();
    }
}
