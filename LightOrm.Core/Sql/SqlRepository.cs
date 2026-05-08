using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using LightOrm.Core.Attributes;
using LightOrm.Core.Models;
using LightOrm.Core.Utilities;

namespace LightOrm.Core.Sql
{
    public class SqlRepository<T, TId> : IRepository<T, TId> where T : BaseModel<T, TId>, new()
    {
        private readonly DbConnection _connection;
        private readonly IDialect _dialect;
        private readonly string _tableName;

        public SqlRepository(DbConnection connection, IDialect dialect)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _dialect = dialect ?? throw new ArgumentNullException(nameof(dialect));
            _tableName = new T().TableName;
        }

        private string Q(string identifier) => _dialect.QuoteIdentifier(identifier);
        private string P(string name) => _dialect.ParameterPrefix + name;

        private async Task EnsureOpenAsync()
        {
            if (_connection.State != ConnectionState.Open)
                await _connection.OpenAsync();
        }

        private DbCommand NewCommand(string sql, DbTransaction tx = null)
        {
            var cmd = _dialect.CreateCommand(_connection);
            cmd.CommandText = sql;
            if (tx != null) cmd.Transaction = tx;
            return cmd;
        }

        private void AddParam(DbCommand cmd, string name, object value, Type clrType)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = _dialect.ParameterPrefix + name;
            p.Value = value == null ? DBNull.Value : (_dialect.ToDbValue(value, clrType) ?? DBNull.Value);
            cmd.Parameters.Add(p);
        }

        public async Task EnsureSchemaAsync()
        {
            await EnsureOpenAsync();
            using var tx = _connection.BeginTransaction();
            try
            {
                using var cmd = NewCommand(BuildCreateTableSql(), tx);
                await cmd.ExecuteNonQueryAsync();
                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        private string BuildCreateTableSql()
        {
            var columns = new List<string>();
            var foreignKeys = new List<string>();

            foreach (var prop in TypeMetadataCache.GetProperties(typeof(T)))
            {
                var col = TypeMetadataCache.GetColumnAttribute(prop);
                if (col == null) continue;

                var sqlType = _dialect.MapType(prop.PropertyType, col);
                var def = $"{Q(col.Name)} {sqlType}";

                if (col.IsPrimaryKey) def += " PRIMARY KEY";
                if (col.AutoIncrement && !string.IsNullOrEmpty(_dialect.AutoIncrementClause))
                    def += " " + _dialect.AutoIncrementClause;

                columns.Add(def);

                var fk = TypeMetadataCache.GetForeignKeyAttribute(prop);
                if (fk != null)
                {
                    foreignKeys.Add(
                        $"FOREIGN KEY ({Q(col.Name)}) REFERENCES {Q(fk.ReferenceTable)}({Q(fk.ReferenceColumn)})");
                }
            }

            var allParts = columns.Concat(foreignKeys);
            var ifNotExists = _dialect.SupportsIfNotExists ? "IF NOT EXISTS " : "";
            var createTable = $"CREATE TABLE {ifNotExists}{Q(_tableName)} (\n  {string.Join(",\n  ", allParts)}\n);";

            // Índices em colunas FK como statements separados (compatível com SQLite,
            // que não aceita INDEX dentro de CREATE TABLE). MySQL também suporta.
            var indexStatements = new List<string>();
            foreach (var prop in TypeMetadataCache.GetProperties(typeof(T)))
            {
                var fk = TypeMetadataCache.GetForeignKeyAttribute(prop);
                var col = TypeMetadataCache.GetColumnAttribute(prop);
                if (fk != null && col != null)
                {
                    var idxIfNotExists = _dialect.SupportsCreateIndexIfNotExists ? "IF NOT EXISTS " : "";
                    indexStatements.Add(
                        $"CREATE INDEX {idxIfNotExists}{Q($"idx_{_tableName}_{col.Name}")} " +
                        $"ON {Q(_tableName)}({Q(col.Name)});");
                }
            }

            return indexStatements.Count == 0
                ? createTable
                : createTable + "\n" + string.Join("\n", indexStatements);
        }

        public async Task<T> SaveAsync(T entity)
        {
            await EnsureOpenAsync();
            var idProp = GetIdProperty();
            var idValue = idProp.GetValue(entity);
            var isNew = IsDefaultId(idValue);

            if (isNew)
            {
                entity.CreatedAt = DateTime.UtcNow;
                entity.UpdatedAt = entity.CreatedAt;
                return await InsertAsync(entity, idProp);
            }
            entity.UpdatedAt = DateTime.UtcNow;
            return await UpdateAsync(entity, idProp);
        }

        private async Task<T> InsertAsync(T entity, PropertyInfo idProp)
        {
            using var tx = _connection.BeginTransaction();
            try
            {
                var writableCols = WritableColumns(skipAutoIncrement: true).ToList();
                var colList = string.Join(", ", writableCols.Select(c => Q(c.col.Name)));
                var paramList = string.Join(", ", writableCols.Select(c => P(c.col.Name)));

                var idCol = TypeMetadataCache.GetColumnAttribute(idProp);
                var returning = IsAutoIncrement(idProp)
                    ? _dialect.GetInsertReturningClause(Q(idCol.Name))
                    : null;

                var sql = string.IsNullOrEmpty(returning)
                    ? $"INSERT INTO {Q(_tableName)} ({colList}) VALUES ({paramList});"
                    : $"INSERT INTO {Q(_tableName)} ({colList}) VALUES ({paramList}) {returning};";

                object rawId = null;
                using (var cmd = NewCommand(sql, tx))
                {
                    foreach (var (col, prop) in writableCols)
                        AddParam(cmd, col.Name, prop.GetValue(entity), prop.PropertyType);

                    if (string.IsNullOrEmpty(returning))
                        await cmd.ExecuteNonQueryAsync();
                    else
                        rawId = await cmd.ExecuteScalarAsync();
                }

                if (IsAutoIncrement(idProp) && string.IsNullOrEmpty(returning))
                {
                    using var idCmd = NewCommand(_dialect.GetLastInsertIdSql(), tx);
                    rawId = await idCmd.ExecuteScalarAsync();
                }

                if (IsAutoIncrement(idProp) && rawId != null && rawId != DBNull.Value)
                {
                    var converted = Convert.ChangeType(rawId,
                        Nullable.GetUnderlyingType(idProp.PropertyType) ?? idProp.PropertyType);
                    idProp.SetValue(entity, converted);
                }

                tx.Commit();
                return entity;
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        private async Task<T> UpdateAsync(T entity, PropertyInfo idProp)
        {
            using var tx = _connection.BeginTransaction();
            try
            {
                var writableCols = WritableColumns(skipAutoIncrement: true)
                    .Where(c => !c.col.IsPrimaryKey)
                    .ToList();
                var setClause = string.Join(", ", writableCols.Select(c => $"{Q(c.col.Name)} = {P(c.col.Name)}"));

                var idCol = TypeMetadataCache.GetColumnAttribute(idProp);
                var sql = $"UPDATE {Q(_tableName)} SET {setClause} WHERE {Q(idCol.Name)} = {P(idCol.Name)}";

                using var cmd = NewCommand(sql, tx);
                foreach (var (col, prop) in writableCols)
                    AddParam(cmd, col.Name, prop.GetValue(entity), prop.PropertyType);
                AddParam(cmd, idCol.Name, idProp.GetValue(entity), idProp.PropertyType);

                await cmd.ExecuteNonQueryAsync();
                tx.Commit();
                return entity;
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        public async Task DeleteAsync(T entity)
        {
            await EnsureOpenAsync();
            var idProp = GetIdProperty();
            var idCol = TypeMetadataCache.GetColumnAttribute(idProp);
            var sql = $"DELETE FROM {Q(_tableName)} WHERE {Q(idCol.Name)} = {P(idCol.Name)}";
            using var cmd = NewCommand(sql);
            AddParam(cmd, idCol.Name, idProp.GetValue(entity), idProp.PropertyType);
            await cmd.ExecuteNonQueryAsync();
        }

        private string SelectColumnList()
        {
            var cols = TypeMetadataCache.GetProperties(typeof(T))
                .Select(p => TypeMetadataCache.GetColumnAttribute(p))
                .Where(c => c != null)
                .Select(c => Q(c.Name));
            return string.Join(", ", cols);
        }

        public async Task<T> FindByIdAsync(TId id, bool includeRelated = false)
        {
            await EnsureOpenAsync();
            var idProp = GetIdProperty();
            var idCol = TypeMetadataCache.GetColumnAttribute(idProp);
            var sql = $"SELECT {SelectColumnList()} FROM {Q(_tableName)} WHERE {Q(idCol.Name)} = {P(idCol.Name)}";
            using var cmd = NewCommand(sql);
            AddParam(cmd, idCol.Name, id, typeof(TId));

            T instance = null;
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                {
                    instance = new T();
                    Populate(reader, instance);
                }
            }

            if (instance != null && includeRelated)
                await RelatedLoader.LoadAsync(_connection, _dialect, typeof(T), new object[] { instance });

            return instance;
        }

        public async Task<List<T>> FindAllAsync(bool includeRelated = false)
        {
            await EnsureOpenAsync();
            var sql = $"SELECT {SelectColumnList()} FROM {Q(_tableName)}";
            var results = new List<T>();
            using (var cmd = NewCommand(sql))
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    var instance = new T();
                    Populate(reader, instance);
                    results.Add(instance);
                }
            }

            if (includeRelated && results.Count > 0)
                await RelatedLoader.LoadAsync(_connection, _dialect, typeof(T), results.Cast<object>().ToList());

            return results;
        }

        public async Task<IReadOnlyList<T>> SaveManyAsync(IEnumerable<T> entities)
        {
            if (entities == null) throw new ArgumentNullException(nameof(entities));
            var list = entities.ToList();
            if (list.Count == 0) return list;

            await EnsureOpenAsync();
            var idProp = GetIdProperty();
            using var tx = _connection.BeginTransaction();
            try
            {
                foreach (var entity in list)
                {
                    var idValue = idProp.GetValue(entity);
                    if (IsDefaultId(idValue))
                    {
                        entity.CreatedAt = DateTime.UtcNow;
                        entity.UpdatedAt = entity.CreatedAt;
                        await InsertWithinTxAsync(entity, idProp, tx);
                    }
                    else
                    {
                        entity.UpdatedAt = DateTime.UtcNow;
                        await UpdateWithinTxAsync(entity, idProp, tx);
                    }
                }
                tx.Commit();
                return list;
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        private async Task InsertWithinTxAsync(T entity, PropertyInfo idProp, DbTransaction tx)
        {
            var writableCols = WritableColumns(skipAutoIncrement: true).ToList();
            var colList = string.Join(", ", writableCols.Select(c => Q(c.col.Name)));
            var paramList = string.Join(", ", writableCols.Select(c => P(c.col.Name)));
            var idCol = TypeMetadataCache.GetColumnAttribute(idProp);
            var returning = IsAutoIncrement(idProp)
                ? _dialect.GetInsertReturningClause(Q(idCol.Name))
                : null;

            var sql = string.IsNullOrEmpty(returning)
                ? $"INSERT INTO {Q(_tableName)} ({colList}) VALUES ({paramList});"
                : $"INSERT INTO {Q(_tableName)} ({colList}) VALUES ({paramList}) {returning};";

            object rawId = null;
            using (var cmd = NewCommand(sql, tx))
            {
                foreach (var (col, prop) in writableCols)
                    AddParam(cmd, col.Name, prop.GetValue(entity), prop.PropertyType);
                if (string.IsNullOrEmpty(returning))
                    await cmd.ExecuteNonQueryAsync();
                else
                    rawId = await cmd.ExecuteScalarAsync();
            }

            if (IsAutoIncrement(idProp) && string.IsNullOrEmpty(returning))
            {
                using var idCmd = NewCommand(_dialect.GetLastInsertIdSql(), tx);
                rawId = await idCmd.ExecuteScalarAsync();
            }

            if (IsAutoIncrement(idProp) && rawId != null && rawId != DBNull.Value)
            {
                var converted = Convert.ChangeType(rawId,
                    Nullable.GetUnderlyingType(idProp.PropertyType) ?? idProp.PropertyType);
                idProp.SetValue(entity, converted);
            }
        }

        private async Task UpdateWithinTxAsync(T entity, PropertyInfo idProp, DbTransaction tx)
        {
            var writableCols = WritableColumns(skipAutoIncrement: true)
                .Where(c => !c.col.IsPrimaryKey)
                .ToList();
            var setClause = string.Join(", ", writableCols.Select(c => $"{Q(c.col.Name)} = {P(c.col.Name)}"));
            var idCol = TypeMetadataCache.GetColumnAttribute(idProp);
            var sql = $"UPDATE {Q(_tableName)} SET {setClause} WHERE {Q(idCol.Name)} = {P(idCol.Name)}";
            using var cmd = NewCommand(sql, tx);
            foreach (var (col, prop) in writableCols)
                AddParam(cmd, col.Name, prop.GetValue(entity), prop.PropertyType);
            AddParam(cmd, idCol.Name, idProp.GetValue(entity), idProp.PropertyType);
            await cmd.ExecuteNonQueryAsync();
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
                try
                {
                    var converted = _dialect.FromDbValue(raw, prop.PropertyType);
                    prop.SetValue(instance, converted);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        $"Falha ao popular {typeof(T).Name}.{prop.Name} (coluna '{col.Name}', " +
                        $"valor '{raw}' do tipo {raw?.GetType().Name ?? "null"}): {ex.Message}", ex);
                }
            }
        }

        private IEnumerable<(ColumnAttribute col, PropertyInfo prop)> WritableColumns(bool skipAutoIncrement)
        {
            foreach (var prop in TypeMetadataCache.GetProperties(typeof(T)))
            {
                var col = TypeMetadataCache.GetColumnAttribute(prop);
                if (col == null) continue;
                if (skipAutoIncrement && col.AutoIncrement) continue;
                yield return (col, prop);
            }
        }

        private static PropertyInfo GetIdProperty()
        {
            foreach (var prop in TypeMetadataCache.GetProperties(typeof(T)))
            {
                var col = TypeMetadataCache.GetColumnAttribute(prop);
                if (col != null && col.IsPrimaryKey) return prop;
            }
            throw new InvalidOperationException($"Type {typeof(T).Name} has no primary key column.");
        }

        private static bool IsAutoIncrement(PropertyInfo idProp)
        {
            var col = TypeMetadataCache.GetColumnAttribute(idProp);
            return col != null && col.AutoIncrement;
        }

        private static bool IsDefaultId(object idValue)
        {
            if (idValue == null) return true;
            var t = idValue.GetType();
            if (t == typeof(int)) return (int)idValue == 0;
            if (t == typeof(long)) return (long)idValue == 0;
            if (t == typeof(short)) return (short)idValue == 0;
            if (t == typeof(Guid)) return (Guid)idValue == Guid.Empty;
            if (t == typeof(string)) return string.IsNullOrEmpty((string)idValue);
            return Equals(idValue, Activator.CreateInstance(t));
        }
    }
}
