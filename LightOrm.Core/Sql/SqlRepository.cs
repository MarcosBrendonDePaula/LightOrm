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
        private readonly DbTransaction _ambientTx;
        private readonly string _tableName;

        public SqlRepository(DbConnection connection, IDialect dialect)
            : this(connection, dialect, null) { }

        // Sobrecarga com transação externa: todas as operações reusam a tx
        // ambiente. Não há commit/rollback automático aqui — quem criou a
        // transação é responsável por finalizar.
        public SqlRepository(DbConnection connection, IDialect dialect, DbTransaction ambientTransaction)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _dialect = dialect ?? throw new ArgumentNullException(nameof(dialect));
            _ambientTx = ambientTransaction;
            _tableName = new T().TableName;
        }

        private bool HasAmbientTx => _ambientTx != null;

        public IQuery<T, TId> Query() => new SqlQuery<T, TId>(_connection, _dialect, _ambientTx);

        // Devolve (tx, owned). Quando há tx ambiente, owned=false e o caller
        // não deve commit/rollback. Quando não há, abre uma nova owned=true.
        private (DbTransaction tx, bool owned) BeginOrUseTx()
        {
            if (_ambientTx != null) return (_ambientTx, false);
            return (_connection.BeginTransaction(), true);
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
                // Executa cada statement (CREATE TABLE + N CREATE INDEX) separadamente
                // em vez de empilhar com ';'. Mais portável entre drivers.
                foreach (var statement in BuildSchemaStatements())
                {
                    using var cmd = NewCommand(statement, tx);
                    await cmd.ExecuteNonQueryAsync();
                }
                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        private IEnumerable<string> BuildSchemaStatements()
        {
            var columns = new List<string>();
            var foreignKeys = new List<string>();

            foreach (var prop in TypeMetadataCache.GetProperties(typeof(T)))
            {
                var col = TypeMetadataCache.GetColumnAttribute(prop);
                if (col == null) continue;
                if (TypeMetadataCache.GetEmbeddedAttribute(prop) != null) continue; // ignora embeds em SQL

                var effective = EffectiveColumn(col, prop.PropertyType);
                var sqlType = _dialect.MapType(prop.PropertyType, effective);
                var def = $"{Q(col.Name)} {sqlType}";

                if (effective.IsPrimaryKey) def += " PRIMARY KEY";
                if (effective.AutoIncrement && !string.IsNullOrEmpty(_dialect.AutoIncrementClause))
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
            yield return $"CREATE TABLE {ifNotExists}{Q(_tableName)} (\n  {string.Join(",\n  ", allParts)}\n)";

            foreach (var prop in TypeMetadataCache.GetProperties(typeof(T)))
            {
                var fk = TypeMetadataCache.GetForeignKeyAttribute(prop);
                var col = TypeMetadataCache.GetColumnAttribute(prop);
                if (fk != null && col != null)
                {
                    var idxIfNotExists = _dialect.SupportsCreateIndexIfNotExists ? "IF NOT EXISTS " : "";
                    yield return $"CREATE INDEX {idxIfNotExists}{Q($"idx_{_tableName}_{col.Name}")} " +
                                 $"ON {Q(_tableName)}({Q(col.Name)})";
                }
            }
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
                // Quando a chave é string/Guid não-autoincrement, geramos id no app.
                AutoGenerateIdIfNeeded(entity, idProp);
                return await InsertAsync(entity, idProp);
            }
            entity.UpdatedAt = DateTime.UtcNow;
            return await UpdateAsync(entity, idProp);
        }

        private static void AutoGenerateIdIfNeeded(T entity, PropertyInfo idProp)
        {
            if (IsAutoIncrement(idProp)) return;
            var t = Nullable.GetUnderlyingType(idProp.PropertyType) ?? idProp.PropertyType;
            if (t == typeof(string))
                idProp.SetValue(entity, Guid.NewGuid().ToString("N"));
            else if (t == typeof(Guid))
                idProp.SetValue(entity, Guid.NewGuid());
        }

        private async Task<T> InsertAsync(T entity, PropertyInfo idProp)
        {
            var (tx, owned) = BeginOrUseTx();
            try
            {
                await InsertWithinTxAsync(entity, idProp, tx);
                if (owned) tx.Commit();
                return entity;
            }
            catch
            {
                if (owned) tx.Rollback();
                throw;
            }
            finally
            {
                if (owned) tx.Dispose();
            }
        }

        private async Task<T> UpdateAsync(T entity, PropertyInfo idProp)
        {
            var (tx, owned) = BeginOrUseTx();
            try
            {
                await UpdateWithinTxAsync(entity, idProp, tx);
                if (owned) tx.Commit();
                return entity;
            }
            catch
            {
                if (owned) tx.Rollback();
                throw;
            }
            finally
            {
                if (owned) tx.Dispose();
            }
        }

        public async Task DeleteAsync(T entity)
        {
            await EnsureOpenAsync();
            var idProp = GetIdProperty();
            var idCol = TypeMetadataCache.GetColumnAttribute(idProp);
            var sql = $"DELETE FROM {Q(_tableName)} WHERE {Q(idCol.Name)} = {P(idCol.Name)}";
            using var cmd = NewCommand(sql, _ambientTx);
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
            using var cmd = NewCommand(sql, _ambientTx);
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
                await RelatedLoader.LoadAsync(_connection, _dialect, typeof(T), new object[] { instance }, _ambientTx);

            return instance;
        }

        public async Task<List<T>> FindAllAsync(bool includeRelated = false)
        {
            await EnsureOpenAsync();
            var sql = $"SELECT {SelectColumnList()} FROM {Q(_tableName)}";
            var results = new List<T>();
            using (var cmd = NewCommand(sql, _ambientTx))
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
                await RelatedLoader.LoadAsync(_connection, _dialect, typeof(T), results.Cast<object>().ToList(), _ambientTx);

            return results;
        }

        public async Task<T> UpsertAsync(T entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            await EnsureOpenAsync();
            var idProp = GetIdProperty();
            var idCol = TypeMetadataCache.GetColumnAttribute(idProp);
            var idValue = idProp.GetValue(entity);

            // Sem id (default ou autoIncrement): cai no caminho normal de Save.
            if (IsDefaultId(idValue))
                return await SaveAsync(entity);

            var (tx, owned) = BeginOrUseTx();
            try
            {
                var checkSql = $"SELECT 1 FROM {Q(_tableName)} WHERE {Q(idCol.Name)} = {P(idCol.Name)}";
                bool exists;
                using (var cmd = NewCommand(checkSql, tx))
                {
                    AddParam(cmd, idCol.Name, idValue, idProp.PropertyType);
                    var raw = await cmd.ExecuteScalarAsync();
                    exists = raw != null && raw != DBNull.Value;
                }

                if (exists)
                {
                    entity.UpdatedAt = DateTime.UtcNow;
                    await UpdateWithinTxAsync(entity, idProp, tx);
                }
                else
                {
                    entity.CreatedAt = DateTime.UtcNow;
                    entity.UpdatedAt = entity.CreatedAt;
                    await InsertWithinTxAsync(entity, idProp, tx);
                }

                if (owned) tx.Commit();
                return entity;
            }
            catch
            {
                if (owned) tx.Rollback();
                throw;
            }
            finally
            {
                if (owned) tx.Dispose();
            }
        }

        public async Task<IReadOnlyList<T>> SaveManyAsync(IEnumerable<T> entities)
        {
            if (entities == null) throw new ArgumentNullException(nameof(entities));
            var list = entities.ToList();
            if (list.Count == 0) return list;

            await EnsureOpenAsync();
            var idProp = GetIdProperty();
            var (tx, owned) = BeginOrUseTx();
            try
            {
                foreach (var entity in list)
                {
                    var idValue = idProp.GetValue(entity);
                    if (IsDefaultId(idValue))
                    {
                        entity.CreatedAt = DateTime.UtcNow;
                        entity.UpdatedAt = entity.CreatedAt;
                        AutoGenerateIdIfNeeded(entity, idProp);
                        await InsertWithinTxAsync(entity, idProp, tx);
                    }
                    else
                    {
                        entity.UpdatedAt = DateTime.UtcNow;
                        await UpdateWithinTxAsync(entity, idProp, tx);
                    }
                }
                if (owned) tx.Commit();
                return list;
            }
            catch
            {
                if (owned) tx.Rollback();
                throw;
            }
            finally
            {
                if (owned) tx.Dispose();
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
                idProp.SetValue(entity, ConvertToTarget(rawId, idProp.PropertyType));
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
                if (TypeMetadataCache.GetEmbeddedAttribute(prop) != null) continue;
                var col = TypeMetadataCache.GetColumnAttribute(prop);
                if (col == null) continue;
                // Usa AutoIncrement efetivo: chave string/Guid com autoIncrement
                // herdado do BaseModel é tratada como não-auto, então o id gerado
                // no app entra no INSERT.
                var effective = EffectiveColumn(col, prop.PropertyType);
                if (skipAutoIncrement && effective.AutoIncrement) continue;
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
            if (col == null || !col.AutoIncrement) return false;
            // AutoIncrement só faz sentido em chaves inteiras. Para string/Guid
            // o valor é gerado no app (ver AutoGenerateIdIfNeeded), e o atributo
            // herdado do BaseModel é ignorado no SQL.
            var t = Nullable.GetUnderlyingType(idProp.PropertyType) ?? idProp.PropertyType;
            return t == typeof(int) || t == typeof(long) || t == typeof(short);
        }

        // Reescreve o ColumnAttribute desligando AutoIncrement para tipos não-inteiros.
        // O atributo herdado de BaseModel<T,TId> tem autoIncrement=true por padrão,
        // mas isso não pode ir pro DDL quando TId é string/Guid.
        private static ColumnAttribute EffectiveColumn(ColumnAttribute col, Type clrType)
        {
            if (!col.AutoIncrement) return col;
            var t = Nullable.GetUnderlyingType(clrType) ?? clrType;
            if (t == typeof(int) || t == typeof(long) || t == typeof(short)) return col;
            return new ColumnAttribute(col.Name, col.IsPrimaryKey, autoIncrement: false,
                                       col.Length, col.IsUnsigned);
        }

        private static object ConvertToTarget(object value, Type targetType)
        {
            var underlying = Nullable.GetUnderlyingType(targetType) ?? targetType;
            if (value.GetType() == underlying) return value;
            return Convert.ChangeType(value, underlying, System.Globalization.CultureInfo.InvariantCulture);
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
