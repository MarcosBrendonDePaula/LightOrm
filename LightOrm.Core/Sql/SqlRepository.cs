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
using LightOrm.Core.Validation;

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

        /// <summary>
        /// Executa SQL bruto e materializa as linhas em T usando o mesmo Populate
        /// dos demais métodos. Parâmetros são passados como pares (nome, valor)
        /// — o caller usa o prefixo do dialect (ex.: "@id" no MySQL).
        /// </summary>
        public async Task<List<T>> RawAsync(string sql, IDictionary<string, object> parameters = null)
        {
            if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentException("SQL vazio.", nameof(sql));
            await EnsureOpenAsync();
            using var cmd = NewCommand(sql, _ambientTx);
            if (parameters != null)
            {
                foreach (var kv in parameters)
                {
                    var p = cmd.CreateParameter();
                    p.ParameterName = kv.Key.StartsWith(_dialect.ParameterPrefix)
                        ? kv.Key
                        : _dialect.ParameterPrefix + kv.Key;
                    p.Value = kv.Value == null ? DBNull.Value
                        : (_dialect.ToDbValue(kv.Value, kv.Value.GetType()) ?? DBNull.Value);
                    cmd.Parameters.Add(p);
                }
            }

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

        /// <summary>
        /// Procura por filtro; se não encontrar, salva defaults.
        /// Útil para "garantir que exista" — common usecase em Sequelize.
        /// </summary>
        public async Task<(T entity, bool created)> FindOrCreateAsync(
            Action<IQuery<T, TId>> filter, T defaults)
        {
            if (filter == null) throw new ArgumentNullException(nameof(filter));
            if (defaults == null) throw new ArgumentNullException(nameof(defaults));

            var q = Query();
            filter(q);
            var existing = await q.FirstOrDefaultAsync();
            if (existing != null) return (existing, false);

            await SaveAsync(defaults);
            return (defaults, true);
        }

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

            // [SoftDelete] adiciona coluna DateTime nullable automaticamente.
            var (sdName, sdProp) = SoftDeleteHelper.Resolve(typeof(T));
            if (sdName != null)
            {
                var sdType = _dialect.MapType(typeof(DateTime?), new ColumnAttribute(sdName));
                columns.Add($"{Q(sdName)} {sdType}");
            }

            var allParts = columns.Concat(foreignKeys);
            var ifNotExists = _dialect.SupportsIfNotExists ? "IF NOT EXISTS " : "";
            yield return $"CREATE TABLE {ifNotExists}{Q(_tableName)} (\n  {string.Join(",\n  ", allParts)}\n)";

            var idxIfNotExists = _dialect.SupportsCreateIndexIfNotExists ? "IF NOT EXISTS " : "";

            // 1) Índice automático em FK (mantém comportamento anterior).
            foreach (var prop in TypeMetadataCache.GetProperties(typeof(T)))
            {
                var fk = TypeMetadataCache.GetForeignKeyAttribute(prop);
                var col = TypeMetadataCache.GetColumnAttribute(prop);
                if (fk != null && col != null)
                {
                    yield return $"CREATE INDEX {idxIfNotExists}{Q($"idx_{_tableName}_{col.Name}")} " +
                                 $"ON {Q(_tableName)}({Q(col.Name)})";
                }
            }

            // 2) [Unique] na propriedade — UNIQUE INDEX dedicado.
            foreach (var prop in TypeMetadataCache.GetProperties(typeof(T)))
            {
                if (TypeMetadataCache.GetUniqueAttribute(prop) == null) continue;
                var col = TypeMetadataCache.GetColumnAttribute(prop);
                if (col == null) continue;
                yield return $"CREATE UNIQUE INDEX {idxIfNotExists}{Q($"uq_{_tableName}_{col.Name}")} " +
                             $"ON {Q(_tableName)}({Q(col.Name)})";
            }

            // 3) [Index] — agrupado por Name. Sem Name vira índice por coluna;
            //    com Name compartilhado, índice composto na ordem de propriedades.
            var indexGroups = new Dictionary<string, (bool unique, List<string> cols)>();
            int anonCounter = 0;
            foreach (var prop in TypeMetadataCache.GetProperties(typeof(T)))
            {
                var idx = TypeMetadataCache.GetIndexAttribute(prop);
                if (idx == null) continue;
                var col = TypeMetadataCache.GetColumnAttribute(prop);
                if (col == null) continue;

                var name = idx.Name ?? $"idx_{_tableName}_{col.Name}_{anonCounter++}";
                if (!indexGroups.TryGetValue(name, out var entry))
                    indexGroups[name] = entry = (idx.Unique, new List<string>());
                else if (entry.unique != idx.Unique)
                    throw new InvalidOperationException(
                        $"Índice '{name}' tem propriedades com Unique inconsistente em {typeof(T).Name}.");
                entry.cols.Add(Q(col.Name));
            }
            foreach (var kv in indexGroups)
            {
                var keyword = kv.Value.unique ? "UNIQUE INDEX" : "INDEX";
                yield return $"CREATE {keyword} {idxIfNotExists}{Q(kv.Key)} " +
                             $"ON {Q(_tableName)}({string.Join(", ", kv.Value.cols)})";
            }
        }

        public async Task<T> SaveAsync(T entity)
        {
            await EnsureOpenAsync();
            var idProp = GetIdProperty();
            var idValue = idProp.GetValue(entity);
            var isNew = IsDefaultId(idValue);

            // Hooks: BeforeSave roda primeiro; depois validação (para que o
            // hook possa ajustar valores antes de validar — ex.: trim em strings).
            entity.OnBeforeSave(isNew);
            await entity.OnBeforeSaveAsync(isNew);
            ModelValidator.Validate(entity);

            T result;
            if (isNew)
            {
                entity.CreatedAt = DateTime.UtcNow;
                entity.UpdatedAt = entity.CreatedAt;
                AutoGenerateIdIfNeeded(entity, idProp);
                result = await InsertAsync(entity, idProp);
            }
            else
            {
                entity.UpdatedAt = DateTime.UtcNow;
                result = await UpdateAsync(entity, idProp);
            }

            entity.OnAfterSave(isNew);
            await entity.OnAfterSaveAsync(isNew);
            return result;
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
                await CascadeSaver.SaveCascadesAsync(_connection, _dialect, tx, typeof(T), entity, idProp);
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
                await CascadeSaver.SaveCascadesAsync(_connection, _dialect, tx, typeof(T), entity, idProp);
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
            entity.OnBeforeDelete();
            await entity.OnBeforeDeleteAsync();

            var idProp = GetIdProperty();
            var idCol = TypeMetadataCache.GetColumnAttribute(idProp);
            var (sdName, sdProp) = SoftDeleteHelper.Resolve(typeof(T));

            string sql;
            if (sdName != null)
            {
                var now = DateTime.UtcNow;
                sdProp.SetValue(entity, now);
                sql = $"UPDATE {Q(_tableName)} SET {Q(sdName)} = {P(sdName)} WHERE {Q(idCol.Name)} = {P(idCol.Name)}";
                using var cmd = NewCommand(sql, _ambientTx);
                AddParam(cmd, sdName, now, typeof(DateTime?));
                AddParam(cmd, idCol.Name, idProp.GetValue(entity), idProp.PropertyType);
                await cmd.ExecuteNonQueryAsync();
            }
            else
            {
                sql = $"DELETE FROM {Q(_tableName)} WHERE {Q(idCol.Name)} = {P(idCol.Name)}";
                using var cmd = NewCommand(sql, _ambientTx);
                AddParam(cmd, idCol.Name, idProp.GetValue(entity), idProp.PropertyType);
                await cmd.ExecuteNonQueryAsync();
            }

            entity.OnAfterDelete();
            await entity.OnAfterDeleteAsync();
        }

        /// <summary>Restaura entidade soft-deletada (zera deleted_at).</summary>
        public async Task RestoreAsync(T entity)
        {
            var (sdName, sdProp) = SoftDeleteHelper.Resolve(typeof(T));
            if (sdName == null)
                throw new InvalidOperationException(
                    $"RestoreAsync requer [SoftDelete] em {typeof(T).Name}.");

            await EnsureOpenAsync();
            var idProp = GetIdProperty();
            var idCol = TypeMetadataCache.GetColumnAttribute(idProp);
            sdProp.SetValue(entity, null);
            var sql = $"UPDATE {Q(_tableName)} SET {Q(sdName)} = NULL WHERE {Q(idCol.Name)} = {P(idCol.Name)}";
            using var cmd = NewCommand(sql, _ambientTx);
            AddParam(cmd, idCol.Name, idProp.GetValue(entity), idProp.PropertyType);
            await cmd.ExecuteNonQueryAsync();
        }

        private string SelectColumnList()
        {
            var cols = TypeMetadataCache.GetProperties(typeof(T))
                .Select(p => TypeMetadataCache.GetColumnAttribute(p))
                .Where(c => c != null)
                .Select(c => Q(c.Name))
                .ToList();
            var (sdName, _) = SoftDeleteHelper.Resolve(typeof(T));
            if (sdName != null) cols.Add(Q(sdName));
            return string.Join(", ", cols);
        }

        public Task<T> FindByIdAsync(TId id, bool includeRelated = false) =>
            FindByIdInternalAsync(id, includeRelated, includeDeleted: false);

        /// <summary>FindById ignorando o filtro de soft delete.</summary>
        public Task<T> FindByIdIncludingDeletedAsync(TId id, bool includeRelated = false) =>
            FindByIdInternalAsync(id, includeRelated, includeDeleted: true);

        private async Task<T> FindByIdInternalAsync(TId id, bool includeRelated, bool includeDeleted)
        {
            await EnsureOpenAsync();
            var idProp = GetIdProperty();
            var idCol = TypeMetadataCache.GetColumnAttribute(idProp);
            var (sdName, _) = SoftDeleteHelper.Resolve(typeof(T));
            var where = $"{Q(idCol.Name)} = {P(idCol.Name)}";
            if (sdName != null && !includeDeleted)
                where += $" AND {Q(sdName)} IS NULL";
            var sql = $"SELECT {SelectColumnList()} FROM {Q(_tableName)} WHERE {where}";
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

            if (instance != null)
            {
                instance.OnAfterLoad();
                await instance.OnAfterLoadAsync();
            }
            return instance;
        }

        public Task<List<T>> FindAllAsync(bool includeRelated = false) =>
            FindAllInternalAsync(includeRelated, includeDeleted: false);

        /// <summary>FindAll ignorando o filtro de soft delete.</summary>
        public Task<List<T>> FindAllIncludingDeletedAsync(bool includeRelated = false) =>
            FindAllInternalAsync(includeRelated, includeDeleted: true);

        private async Task<List<T>> FindAllInternalAsync(bool includeRelated, bool includeDeleted)
        {
            await EnsureOpenAsync();
            var (sdName, _) = SoftDeleteHelper.Resolve(typeof(T));
            var sql = $"SELECT {SelectColumnList()} FROM {Q(_tableName)}";
            if (sdName != null && !includeDeleted)
                sql += $" WHERE {Q(sdName)} IS NULL";
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

            foreach (var r in results)
            {
                r.OnAfterLoad();
                await r.OnAfterLoadAsync();
            }

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
            // Inicializa coluna [Version] em 1 se ainda estiver no default (0).
            var versionProp = GetVersionProperty();
            if (versionProp != null)
            {
                var current = versionProp.GetValue(entity);
                if (current is int i && i == 0) versionProp.SetValue(entity, 1);
                else if (current is long l && l == 0L) versionProp.SetValue(entity, 1L);
            }

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
            var versionProp = GetVersionProperty();
            object oldVersion = null;
            string versionColName = null;

            if (versionProp != null)
            {
                versionColName = TypeMetadataCache.GetColumnAttribute(versionProp).Name;
                oldVersion = versionProp.GetValue(entity);
                // Incrementa antes do SET; o WHERE usa oldVersion.
                if (oldVersion is int i) versionProp.SetValue(entity, i + 1);
                else if (oldVersion is long l) versionProp.SetValue(entity, l + 1L);
            }

            var writableCols = WritableColumns(skipAutoIncrement: true)
                .Where(c => !c.col.IsPrimaryKey)
                .ToList();
            var setClause = string.Join(", ", writableCols.Select(c => $"{Q(c.col.Name)} = {P(c.col.Name)}"));
            var idCol = TypeMetadataCache.GetColumnAttribute(idProp);

            var whereClause = $"{Q(idCol.Name)} = {P(idCol.Name)}";
            if (versionProp != null)
                whereClause += $" AND {Q(versionColName)} = {P("__oldVersion")}";

            var sql = $"UPDATE {Q(_tableName)} SET {setClause} WHERE {whereClause}";
            using var cmd = NewCommand(sql, tx);
            foreach (var (col, prop) in writableCols)
                AddParam(cmd, col.Name, prop.GetValue(entity), prop.PropertyType);
            AddParam(cmd, idCol.Name, idProp.GetValue(entity), idProp.PropertyType);
            if (versionProp != null)
                AddParam(cmd, "__oldVersion", oldVersion, versionProp.PropertyType);

            var rows = await cmd.ExecuteNonQueryAsync();
            if (versionProp != null && rows == 0)
            {
                // Reverte o incremento em memória pra evitar estado inconsistente.
                if (oldVersion is int i) versionProp.SetValue(entity, i);
                else if (oldVersion is long l) versionProp.SetValue(entity, l);
                throw new DbConcurrencyException(
                    $"Conflito de versão em {typeof(T).Name} (id={idProp.GetValue(entity)}). " +
                    $"Outra escrita modificou ou apagou a linha desde a leitura.");
            }
        }

        private static PropertyInfo GetVersionProperty()
        {
            foreach (var prop in TypeMetadataCache.GetProperties(typeof(T)))
            {
                if (TypeMetadataCache.GetVersionAttribute(prop) != null)
                {
                    var t = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
                    if (t != typeof(int) && t != typeof(long))
                        throw new InvalidOperationException(
                            $"[Version] em {typeof(T).Name}.{prop.Name} requer int ou long; recebido {t.Name}.");
                    return prop;
                }
            }
            return null;
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

            // SoftDelete: popula DeletedAt sem [Column] na propriedade.
            var (sdName, sdProp) = SoftDeleteHelper.Resolve(typeof(T));
            if (sdName != null)
            {
                try
                {
                    int ord = reader.GetOrdinal(sdName);
                    if (!reader.IsDBNull(ord))
                    {
                        var raw = reader.GetValue(ord);
                        sdProp.SetValue(instance, _dialect.FromDbValue(raw, typeof(DateTime?)));
                    }
                }
                catch (IndexOutOfRangeException) { /* coluna ausente, ignora */ }
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
            // SoftDelete: DeletedAt entra no INSERT/UPDATE como coluna virtual.
            var (sdName, sdProp) = SoftDeleteHelper.Resolve(typeof(T));
            if (sdName != null)
                yield return (new ColumnAttribute(sdName), sdProp);
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
