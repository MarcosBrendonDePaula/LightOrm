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

        // Adiciona um grupo (cond1 OR cond2 OR ...) ao WHERE. As condições do
        // grupo são combinadas com OR; o grupo se combina com os demais via AND.
        // Exemplo: .Where("Active", true).WhereAny(("Role","=","admin"), ("Role","=","mod"))
        // → WHERE Active = ? AND (Role = ? OR Role = ?)
        IQuery<T, TId> WhereAny(params (string property, string op, object value)[] conditions);
        IQuery<T, TId> OrderBy(string propertyName);
        IQuery<T, TId> OrderByDescending(string propertyName);
        IQuery<T, TId> Take(int limit);
        IQuery<T, TId> Skip(int offset);

        Task<List<T>> ToListAsync();
        Task<T> FirstOrDefaultAsync();
        Task<int> CountAsync();
        Task<bool> AnyAsync();

        // Bulk update: aplica os pares (propriedade → valor) a todas as linhas
        // que casam o filtro. Devolve o número de linhas afetadas.
        Task<int> UpdateAsync(System.Collections.Generic.IDictionary<string, object> set);

        // Bulk delete: remove (ou marca como deleted, em soft-delete) todas as
        // linhas que casam o filtro. Devolve o número de linhas afetadas.
        Task<int> DeleteAsync();
    }
}
