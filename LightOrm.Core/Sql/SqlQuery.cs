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

        // Cada item é uma condição simples OR um grupo OR. Grupos têm Operator="OR_GROUP"
        // e Value é um List<(string col, string op, object val)>.
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

        public IQuery<T, TId> WhereAny(params (string property, string op, object value)[] conditions)
        {
            if (conditions == null || conditions.Length == 0)
                throw new ArgumentException("WhereAny requer ao menos uma condição.", nameof(conditions));
            var resolved = new List<(string, string, object)>();
            foreach (var c in conditions)
            {
                ValidateOperator(c.op);
                resolved.Add((ResolveColumnName(c.property), c.op, c.value));
            }
            _conditions.Add(("__OR_GROUP", "OR_GROUP", resolved));
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
            foreach (var r in results)
            {
                r.OnAfterLoad();
                await r.OnAfterLoadAsync();
            }
            return results;
        }

        public async Task<T> FirstOrDefaultAsync()
        {
            var previousLimit = _limit;
            try
            {
                _limit = 1;
                var list = await ToListAsync();
                return list.Count == 0 ? null : list[0];
            }
            finally
            {
                _limit = previousLimit;
            }
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
            var previousLimit = _limit;
            try
            {
                _limit = 1;
                return await CountAsync() > 0;
            }
            finally
            {
                _limit = previousLimit;
            }
        }

        public async Task<int> UpdateAsync(IDictionary<string, object> set)
        {
            if (set == null || set.Count == 0)
                throw new ArgumentException("UpdateAsync requer ao menos um par.", nameof(set));

            await EnsureOpenAsync();

            var setPairs = new List<(string col, object val, Type type)>();
            foreach (var kv in set)
            {
                var colName = ResolveColumnName(kv.Key);
                setPairs.Add((colName, kv.Value, kv.Value?.GetType() ?? typeof(object)));
            }

            var sb = new System.Text.StringBuilder();
            sb.Append("UPDATE ").Append(Q(_tableName)).Append(" SET ");
            var setSql = new List<string>();
            var parameters = new List<(string name, object value, Type type)>();
            int idx = 0;
            foreach (var (col, val, type) in setPairs)
            {
                var pname = $"set{idx++}";
                setSql.Add($"{Q(col)} = {P(pname)}");
                parameters.Add((pname, val, type));
            }
            sb.Append(string.Join(", ", setSql));

            var (selectSql, whereParams) = BuildSelectSql("*");
            int whereIdx = selectSql.IndexOf(" WHERE ", StringComparison.Ordinal);
            if (whereIdx >= 0)
            {
                sb.Append(selectSql.Substring(whereIdx));
                parameters.AddRange(whereParams);
            }

            using var cmd = _dialect.CreateCommand(_connection);
            cmd.CommandText = sb.ToString();
            if (_ambientTx != null) cmd.Transaction = _ambientTx;
            ApplyParameters(cmd, parameters);
            return await cmd.ExecuteNonQueryAsync();
        }

        public async Task<int> DeleteAsync()
        {
            await EnsureOpenAsync();

            var (sdName, _) = SoftDeleteHelper.Resolve(typeof(T));
            if (sdName != null && !IncludeDeleted)
            {
                // Soft delete em massa: SET deleted_at = now(). Não reusa
                // UpdateAsync porque a coluna deleted_at não tem [Column]
                // declarado nas propriedades do modelo (é virtual).
                var (selectSqlSd, whereParamsSd) = BuildSelectSql("*");
                var sbSd = new System.Text.StringBuilder();
                sbSd.Append("UPDATE ").Append(Q(_tableName))
                    .Append(" SET ").Append(Q(sdName)).Append(" = ").Append(P("__sdNow"));
                int idxSd = selectSqlSd.IndexOf(" WHERE ", StringComparison.Ordinal);
                if (idxSd >= 0) sbSd.Append(selectSqlSd.Substring(idxSd));

                using var cmdSd = _dialect.CreateCommand(_connection);
                cmdSd.CommandText = sbSd.ToString();
                if (_ambientTx != null) cmdSd.Transaction = _ambientTx;
                var pSd = cmdSd.CreateParameter();
                pSd.ParameterName = P("__sdNow");
                pSd.Value = _dialect.ToDbValue(DateTime.UtcNow, typeof(DateTime?)) ?? DBNull.Value;
                cmdSd.Parameters.Add(pSd);
                ApplyParameters(cmdSd, whereParamsSd);
                return await cmdSd.ExecuteNonQueryAsync();
            }

            var sb = new System.Text.StringBuilder();
            sb.Append("DELETE FROM ").Append(Q(_tableName));

            var (selectSql, whereParams) = BuildSelectSql("*");
            int whereIdx = selectSql.IndexOf(" WHERE ", StringComparison.Ordinal);
            if (whereIdx >= 0) sb.Append(selectSql.Substring(whereIdx));

            using var cmd = _dialect.CreateCommand(_connection);
            cmd.CommandText = sb.ToString();
            if (_ambientTx != null) cmd.Transaction = _ambientTx;
            ApplyParameters(cmd, whereParams);
            return await cmd.ExecuteNonQueryAsync();
        }

        public Task<decimal?> SumAsync(string propertyName) => ScalarAggregateAsync("SUM", propertyName);
        public Task<decimal?> AvgAsync(string propertyName) => ScalarAggregateAsync("AVG", propertyName);
        public Task<decimal?> MinAsync(string propertyName) => ScalarAggregateAsync("MIN", propertyName);
        public Task<decimal?> MaxAsync(string propertyName) => ScalarAggregateAsync("MAX", propertyName);

        private async Task<decimal?> ScalarAggregateAsync(string func, string propertyName)
        {
            await EnsureOpenAsync();
            var col = ResolveColumnName(propertyName);
            var (sql, parameters) = BuildSelectSql($"{func}({Q(col)})");
            sql = StripOrderAndPaging(sql);

            using var cmd = _dialect.CreateCommand(_connection);
            cmd.CommandText = sql;
            if (_ambientTx != null) cmd.Transaction = _ambientTx;
            ApplyParameters(cmd, parameters);
            var raw = await cmd.ExecuteScalarAsync();
            if (raw == null || raw == DBNull.Value) return null;
            try
            {
                return Convert.ToDecimal(raw, System.Globalization.CultureInfo.InvariantCulture);
            }
            catch (Exception ex) when (ex is InvalidCastException || ex is FormatException)
            {
                // DateTime pode vir como DateTime nativo ou string formatada pelo dialect.
                if (raw is DateTime dt) return dt.Ticks;
                if (DateTime.TryParse(raw.ToString(), System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var parsed))
                    return parsed.Ticks;
                throw;
            }
        }

        public async Task<List<(object key, int count)>> GroupByAsync(string propertyName)
        {
            await EnsureOpenAsync();
            var col = ResolveColumnName(propertyName);
            // Tira ORDER/LIMIT do query base, depois acrescenta GROUP BY.
            var (sql, parameters) = BuildSelectSql($"{Q(col)} AS __key, COUNT(*) AS __count");
            sql = StripOrderAndPaging(sql);
            sql += $" GROUP BY {Q(col)}";

            using var cmd = _dialect.CreateCommand(_connection);
            cmd.CommandText = sql;
            if (_ambientTx != null) cmd.Transaction = _ambientTx;
            ApplyParameters(cmd, parameters);

            var result = new List<(object, int)>();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var key = reader.IsDBNull(0) ? null : reader.GetValue(0);
                var count = Convert.ToInt32(reader.GetValue(1));
                result.Add((key, count));
            }
            return result;
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

        // Permite que o repositório oferecer "IncludingDeleted" desligue o filtro.
        internal bool IncludeDeleted { get; set; }

        private (string sql, List<(string name, object value, Type type)> parameters) BuildSelectSql(string projection)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("SELECT ").Append(projection).Append(" FROM ").Append(Q(_tableName));

            var parameters = new List<(string, object, Type)>();
            var (sdName, _) = SoftDeleteHelper.Resolve(typeof(T));
            var hasSoftFilter = sdName != null && !IncludeDeleted;

            if (_conditions.Count > 0 || hasSoftFilter)
            {
                sb.Append(" WHERE ");
                if (hasSoftFilter)
                {
                    sb.Append(Q(sdName)).Append(" IS NULL");
                    if (_conditions.Count > 0) sb.Append(" AND ");
                }
            }
            if (_conditions.Count > 0)
            {
                for (int i = 0; i < _conditions.Count; i++)
                {
                    var (column, op, value) = _conditions[i];
                    if (i > 0) sb.Append(" AND ");
                    if (op == "IN")
                    {
                        var values = (List<object>)value;
                        if (values.Count == 0)
                        {
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
                    else if (op == "OR_GROUP")
                    {
                        var group = (List<(string col, string op, object val)>)value;
                        sb.Append('(');
                        for (int j = 0; j < group.Count; j++)
                        {
                            var (gc, go, gv) = group[j];
                            if (j > 0) sb.Append(" OR ");
                            var pname = $"p{i}_{j}";
                            sb.Append(Q(gc)).Append(' ').Append(go).Append(' ').Append(P(pname));
                            parameters.Add((pname, gv, gv?.GetType() ?? typeof(object)));
                        }
                        sb.Append(')');
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
