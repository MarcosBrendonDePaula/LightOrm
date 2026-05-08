using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using LightOrm.Core.Attributes;
using LightOrm.Core.Models;
using LightOrm.Core.Utilities;

namespace LightOrm.Core.Sql
{
    /// <summary>
    /// Builder fluente para consultas além de FindById/FindAll. Usa nomes de
    /// propriedades CLR (preferencialmente via nameof) e traduz para nome de
    /// coluna pelo ColumnAttribute. Operadores e valores ficam parametrizados —
    /// o nome da propriedade é validado contra o modelo, então não há injeção
    /// possível pelo nome.
    /// </summary>
    public class SqlQuery<T, TId> : IQuery<T, TId> where T : BaseModel<T, TId>, new()
    {
        private readonly DbConnection _connection;
        private readonly IDialect _dialect;
        private readonly DbTransaction _ambientTx;
        private readonly string _tableName;

        private readonly List<(string column, string op, object value)> _conditions = new List<(string, string, object)>();
        private readonly List<(string column, bool desc)> _orderBy = new List<(string, bool)>();
        private int? _limit;
        private int? _offset;

        internal SqlQuery(DbConnection connection, IDialect dialect, DbTransaction ambientTx)
        {
            _connection = connection;
            _dialect = dialect;
            _ambientTx = ambientTx;
            _tableName = new T().TableName;
        }

        public IQuery<T, TId> Where(string propertyName, string op, object value)
        {
            ValidateOperator(op);
            _conditions.Add((ResolveColumnName(propertyName), op, value));
            return this;
        }

        public IQuery<T, TId> Where(string propertyName, object value) =>
            Where(propertyName, "=", value);

        public IQuery<T, TId> WhereIn(string propertyName, IEnumerable<object> values)
        {
            _conditions.Add((ResolveColumnName(propertyName), "IN", values?.ToList() ?? new List<object>()));
            return this;
        }

        public IQuery<T, TId> OrderBy(string propertyName)
        {
            _orderBy.Add((ResolveColumnName(propertyName), false));
            return this;
        }

        public IQuery<T, TId> OrderByDescending(string propertyName)
        {
            _orderBy.Add((ResolveColumnName(propertyName), true));
            return this;
        }

        public IQuery<T, TId> Take(int limit)
        {
            if (limit < 0) throw new ArgumentOutOfRangeException(nameof(limit));
            _limit = limit;
            return this;
        }

        public IQuery<T, TId> Skip(int offset)
        {
            if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset));
            _offset = offset;
            return this;
        }

        public async Task<List<T>> ToListAsync()
        {
            await EnsureOpenAsync();
            var (sql, parameters) = BuildSelectSql(BuildSelectColumns());
            using var cmd = _dialect.CreateCommand(_connection);
            cmd.CommandText = sql;
            if (_ambientTx != null) cmd.Transaction = _ambientTx;
            ApplyParameters(cmd, parameters);

            var results = new List<T>();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var instance = new T();
                Populate(reader, instance);
                results.Add(instance);
            }
            return results;
        }

        public async Task<T> FirstOrDefaultAsync()
        {
            _limit = 1;
            var list = await ToListAsync();
            return list.Count == 0 ? null : list[0];
        }

        public async Task<int> CountAsync()
        {
            await EnsureOpenAsync();
            var (sql, parameters) = BuildSelectSql("COUNT(*)");
            // COUNT ignora ORDER BY/LIMIT por simplicidade — quem quiser
            // contar paginado faz outra query.
            sql = StripOrderAndPaging(sql);
            using var cmd = _dialect.CreateCommand(_connection);
            cmd.CommandText = sql;
            if (_ambientTx != null) cmd.Transaction = _ambientTx;
            ApplyParameters(cmd, parameters);
            var raw = await cmd.ExecuteScalarAsync();
            return Convert.ToInt32(raw);
        }

        public async Task<bool> AnyAsync()
        {
            _limit = 1;
            return await CountAsync() > 0;
        }

        // ------- internals -------

        private async Task EnsureOpenAsync()
        {
            if (_connection.State != System.Data.ConnectionState.Open)
                await _connection.OpenAsync();
        }

        private string Q(string id) => _dialect.QuoteIdentifier(id);
        private string P(string name) => _dialect.ParameterPrefix + name;

        private string BuildSelectColumns()
        {
            var cols = TypeMetadataCache.GetProperties(typeof(T))
                .Select(p => TypeMetadataCache.GetColumnAttribute(p))
                .Where(c => c != null)
                .Select(c => Q(c.Name));
            return string.Join(", ", cols);
        }

        private (string sql, List<(string name, object value, Type type)> parameters) BuildSelectSql(string projection)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("SELECT ").Append(projection).Append(" FROM ").Append(Q(_tableName));

            var parameters = new List<(string, object, Type)>();

            if (_conditions.Count > 0)
            {
                sb.Append(" WHERE ");
                for (int i = 0; i < _conditions.Count; i++)
                {
                    var (column, op, value) = _conditions[i];
                    if (i > 0) sb.Append(" AND ");
                    if (op == "IN")
                    {
                        var values = (List<object>)value;
                        if (values.Count == 0)
                        {
                            // IN () é inválido em SQL; gera condição sempre falsa.
                            sb.Append("1 = 0");
                            continue;
                        }
                        var names = new List<string>();
                        for (int j = 0; j < values.Count; j++)
                        {
                            var pname = $"p{i}_{j}";
                            names.Add(P(pname));
                            parameters.Add((pname, values[j], values[j]?.GetType() ?? typeof(object)));
                        }
                        sb.Append(Q(column)).Append(" IN (").Append(string.Join(", ", names)).Append(')');
                    }
                    else
                    {
                        var pname = $"p{i}";
                        sb.Append(Q(column)).Append(' ').Append(op).Append(' ').Append(P(pname));
                        parameters.Add((pname, value, value?.GetType() ?? typeof(object)));
                    }
                }
            }

            if (_orderBy.Count > 0)
            {
                sb.Append(" ORDER BY ");
                sb.Append(string.Join(", ",
                    _orderBy.Select(o => Q(o.column) + (o.desc ? " DESC" : " ASC"))));
            }

            if (_limit.HasValue) sb.Append(" LIMIT ").Append(_limit.Value);
            if (_offset.HasValue) sb.Append(" OFFSET ").Append(_offset.Value);

            return (sb.ToString(), parameters);
        }

        private static string StripOrderAndPaging(string sql)
        {
            int idx = sql.IndexOf(" ORDER BY ", StringComparison.Ordinal);
            if (idx > 0) sql = sql.Substring(0, idx);
            idx = sql.IndexOf(" LIMIT ", StringComparison.Ordinal);
            if (idx > 0) sql = sql.Substring(0, idx);
            return sql;
        }

        private void ApplyParameters(DbCommand cmd, List<(string name, object value, Type type)> parameters)
        {
            foreach (var (name, value, type) in parameters)
            {
                var p = cmd.CreateParameter();
                p.ParameterName = P(name);
                p.Value = value == null ? DBNull.Value : (_dialect.ToDbValue(value, type) ?? DBNull.Value);
                cmd.Parameters.Add(p);
            }
        }

        private void Populate(DbDataReader reader, T instance)
        {
            foreach (var prop in TypeMetadataCache.GetProperties(typeof(T)))
            {
                var col = TypeMetadataCache.GetColumnAttribute(prop);
                if (col == null || !prop.CanWrite) continue;
                int ordinal;
                try { ordinal = reader.GetOrdinal(col.Name); }
                catch (IndexOutOfRangeException) { continue; }
                if (reader.IsDBNull(ordinal)) continue;
                var raw = reader.GetValue(ordinal);
                var converted = _dialect.FromDbValue(raw, prop.PropertyType);
                prop.SetValue(instance, converted);
            }
        }

        private static string ResolveColumnName(string propertyOrColumnName)
        {
            if (string.IsNullOrEmpty(propertyOrColumnName))
                throw new ArgumentException("Nome da propriedade não pode ser vazio.", nameof(propertyOrColumnName));

            foreach (var prop in TypeMetadataCache.GetProperties(typeof(T)))
            {
                var col = TypeMetadataCache.GetColumnAttribute(prop);
                if (col == null) continue;
                if (prop.Name == propertyOrColumnName) return col.Name;
                if (col.Name == propertyOrColumnName) return col.Name;
            }
            throw new ArgumentException(
                $"Propriedade ou coluna '{propertyOrColumnName}' não encontrada em {typeof(T).Name}.",
                nameof(propertyOrColumnName));
        }

        private static readonly HashSet<string> AllowedOperators = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "=", "!=", "<>", "<", "<=", ">", ">=", "LIKE", "NOT LIKE", "IS", "IS NOT"
        };

        private static void ValidateOperator(string op)
        {
            if (string.IsNullOrEmpty(op) || !AllowedOperators.Contains(op))
                throw new ArgumentException(
                    $"Operador '{op}' não suportado. Use um de: {string.Join(", ", AllowedOperators)}",
                    nameof(op));
        }
    }
}
