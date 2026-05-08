using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using LightOrm.Core.Attributes;
using LightOrm.Core.Models;
using LightOrm.Core.Utilities;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace LightOrm.Mongo
{
    public class MongoRepository<T, TId> : IRepository<T, TId> where T : BaseModel<T, TId>, new()
    {
        private readonly IMongoCollection<BsonDocument> _collection;
        private readonly PropertyInfo _idProp;
        private readonly string _idColumnName;
        private readonly string _softDeleteField;     // null se [SoftDelete] não está ativo
        private readonly PropertyInfo _softDeleteProp;
        private readonly PropertyInfo _versionProp;   // null se [Version] não está ativo
        private readonly string _versionField;

        public MongoRepository(IMongoDatabase database)
        {
            if (database == null) throw new ArgumentNullException(nameof(database));
            var tableName = new T().TableName;
            _collection = database.GetCollection<BsonDocument>(tableName);
            _idProp = ResolveIdProperty();
            _idColumnName = TypeMetadataCache.GetColumnAttribute(_idProp).Name;

            var sdAttr = TypeMetadataCache.GetSoftDeleteAttribute(typeof(T));
            if (sdAttr != null)
            {
                _softDeleteField = sdAttr.ColumnName;
                _softDeleteProp = typeof(T).GetProperty("DeletedAt")
                    ?? throw new InvalidOperationException(
                        $"[SoftDelete] em {typeof(T).Name}: propriedade DeletedAt ausente.");
            }

            foreach (var prop in TypeMetadataCache.GetProperties(typeof(T)))
            {
                if (TypeMetadataCache.GetVersionAttribute(prop) != null)
                {
                    var t = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
                    if (t != typeof(int) && t != typeof(long))
                        throw new InvalidOperationException(
                            $"[Version] em {typeof(T).Name}.{prop.Name} requer int ou long.");
                    _versionProp = prop;
                    var col = TypeMetadataCache.GetColumnAttribute(prop);
                    _versionField = col?.Name ?? prop.Name;
                    break;
                }
            }
        }

        public Task EnsureSchemaAsync() => Task.CompletedTask; // Mongo é schemaless.

        public IQuery<T, TId> Query() => new MongoQuery<T, TId>(_collection, FromBson);

        public async Task<(T entity, bool created)> FindOrCreateAsync(Action<IQuery<T, TId>> filter, T defaults)
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

        public async Task<T> UpsertAsync(T entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            var idValue = _idProp.GetValue(entity);
            var now = DateTime.UtcNow;

            if (IsDefaultId(idValue))
            {
                // Sem id: gera e insere — comportamento idêntico a SaveAsync de novo registro.
                return await SaveAsync(entity);
            }

            // Id presente: replace com upsert. Se o registro não existir,
            // CreatedAt/UpdatedAt são definidos como agora; se existir, mantém
            // o CreatedAt original e atualiza UpdatedAt.
            var filter = Builders<BsonDocument>.Filter.Eq("_id", BsonValue.Create(idValue));
            var existing = await _collection.Find(filter).FirstOrDefaultAsync();
            if (existing == null)
            {
                entity.CreatedAt = now;
                entity.UpdatedAt = now;
            }
            else
            {
                if (existing.Contains("CreatedAt"))
                {
                    var raw = MongoDB.Bson.BsonTypeMapper.MapToDotNetValue(existing["CreatedAt"]);
                    if (raw is DateTime dt) entity.CreatedAt = dt;
                }
                entity.UpdatedAt = now;
            }
            var doc = ToBson(entity);
            await _collection.ReplaceOneAsync(filter, doc, new ReplaceOptions { IsUpsert = true });
            return entity;
        }

        public async Task<System.Collections.Generic.IReadOnlyList<T>> SaveManyAsync(System.Collections.Generic.IEnumerable<T> entities)
        {
            if (entities == null) throw new ArgumentNullException(nameof(entities));
            var list = new System.Collections.Generic.List<T>();
            var inserts = new System.Collections.Generic.List<BsonDocument>();
            var insertEntities = new System.Collections.Generic.List<T>();
            var now = DateTime.UtcNow;

            foreach (var entity in entities)
            {
                var idValue = _idProp.GetValue(entity);
                if (IsDefaultId(idValue))
                {
                    entity.CreatedAt = now;
                    entity.UpdatedAt = now;
                    if (typeof(TId) == typeof(string) && (idValue == null || (string)idValue == ""))
                        _idProp.SetValue(entity, ObjectId.GenerateNewId().ToString());
                    else if (typeof(TId) == typeof(Guid) && (Guid)idValue == Guid.Empty)
                        _idProp.SetValue(entity, Guid.NewGuid());

                    inserts.Add(ToBson(entity));
                    insertEntities.Add(entity);
                }
                else
                {
                    // Updates ainda vão um por vez — InsertMany é só pra novos.
                    await SaveAsync(entity);
                }
                list.Add(entity);
            }

            if (inserts.Count > 0)
                await _collection.InsertManyAsync(inserts);

            return list;
        }

        public async Task<T> SaveAsync(T entity)
        {
            var idValue = _idProp.GetValue(entity);
            var isNew = IsDefaultId(idValue);
            var now = DateTime.UtcNow;

            entity.OnBeforeSave(isNew);
            await entity.OnBeforeSaveAsync(isNew);
            LightOrm.Core.Validation.ModelValidator.Validate(entity);

            if (isNew)
            {
                entity.CreatedAt = now;
                entity.UpdatedAt = now;
                if (typeof(TId) == typeof(string) && (idValue == null || (string)idValue == ""))
                {
                    var generated = ObjectId.GenerateNewId().ToString();
                    _idProp.SetValue(entity, generated);
                }
                else if (typeof(TId) == typeof(Guid) && (Guid)idValue == Guid.Empty)
                {
                    _idProp.SetValue(entity, Guid.NewGuid());
                }
                if (_versionProp != null)
                {
                    var current = _versionProp.GetValue(entity);
                    if (current is int i && i == 0) _versionProp.SetValue(entity, 1);
                    else if (current is long l && l == 0L) _versionProp.SetValue(entity, 1L);
                }
                var doc = ToBson(entity);
                await _collection.InsertOneAsync(doc);
            }
            else if (_versionProp != null)
            {
                // Update com optimistic locking: filter por _id + versão atual.
                entity.UpdatedAt = now;
                var oldVersion = _versionProp.GetValue(entity);
                if (oldVersion is int i) _versionProp.SetValue(entity, i + 1);
                else if (oldVersion is long l) _versionProp.SetValue(entity, l + 1L);
                var doc = ToBson(entity);
                var filter = Builders<BsonDocument>.Filter.And(
                    Builders<BsonDocument>.Filter.Eq("_id", doc["_id"]),
                    Builders<BsonDocument>.Filter.Eq(_versionField, BsonValue.Create(oldVersion)));
                var result = await _collection.ReplaceOneAsync(filter, doc);
                if (result.MatchedCount == 0)
                {
                    // Reverte versão em memória pra permitir reload + retry.
                    if (oldVersion is int i2) _versionProp.SetValue(entity, i2);
                    else if (oldVersion is long l2) _versionProp.SetValue(entity, l2);
                    throw new LightOrm.Core.Sql.DbConcurrencyException(
                        $"Conflito de versão em {typeof(T).Name} (id={idValue}). " +
                        $"Outra escrita modificou ou apagou o documento desde a leitura.");
                }
            }
            else
            {
                entity.UpdatedAt = now;
                var doc = ToBson(entity);
                var filter = Builders<BsonDocument>.Filter.Eq("_id", doc["_id"]);
                await _collection.ReplaceOneAsync(filter, doc, new ReplaceOptions { IsUpsert = true });
            }

            entity.OnAfterSave(isNew);
            await entity.OnAfterSaveAsync(isNew);
            return entity;
        }

        public Task<T> FindByIdAsync(TId id, bool includeRelated = false) =>
            FindByIdInternalAsync(id, includeDeleted: false);

        private async Task<T> FindByIdInternalAsync(TId id, bool includeDeleted)
        {
            var filter = Builders<BsonDocument>.Filter.Eq("_id", BsonValue.Create(id));
            if (_softDeleteField != null && !includeDeleted)
                filter = Builders<BsonDocument>.Filter.And(filter,
                    Builders<BsonDocument>.Filter.Eq(_softDeleteField, BsonNull.Value));
            var doc = await _collection.Find(filter).FirstOrDefaultAsync();
            if (doc == null) return null;
            var instance = FromBson(doc);
            instance.OnAfterLoad();
            await instance.OnAfterLoadAsync();
            return instance;
        }

        public Task<List<T>> FindAllAsync(bool includeRelated = false) =>
            FindAllInternalAsync(includeDeleted: false);

        private async Task<List<T>> FindAllInternalAsync(bool includeDeleted)
        {
            var filter = FilterDefinition<BsonDocument>.Empty;
            if (_softDeleteField != null && !includeDeleted)
                filter = Builders<BsonDocument>.Filter.Eq(_softDeleteField, BsonNull.Value);
            var docs = await _collection.Find(filter).ToListAsync();
            var results = docs.Select(FromBson).ToList();
            foreach (var r in results)
            {
                r.OnAfterLoad();
                await r.OnAfterLoadAsync();
            }
            return results;
        }

        public async Task DeleteAsync(T entity)
        {
            entity.OnBeforeDelete();
            await entity.OnBeforeDeleteAsync();
            var idValue = _idProp.GetValue(entity);
            var filter = Builders<BsonDocument>.Filter.Eq("_id", BsonValue.Create(idValue));

            if (_softDeleteField != null)
            {
                var now = DateTime.UtcNow;
                _softDeleteProp.SetValue(entity, (DateTime?)now);
                var update = Builders<BsonDocument>.Update.Set(_softDeleteField, BsonValue.Create(now));
                await _collection.UpdateOneAsync(filter, update);
            }
            else
            {
                await _collection.DeleteOneAsync(filter);
            }

            entity.OnAfterDelete();
            await entity.OnAfterDeleteAsync();
        }

        /// <summary>Restaura entidade soft-deletada (zera deleted_at).</summary>
        public async Task RestoreAsync(T entity)
        {
            if (_softDeleteField == null)
                throw new InvalidOperationException(
                    $"RestoreAsync requer [SoftDelete] em {typeof(T).Name}.");
            var idValue = _idProp.GetValue(entity);
            var filter = Builders<BsonDocument>.Filter.Eq("_id", BsonValue.Create(idValue));
            var update = Builders<BsonDocument>.Update.Set(_softDeleteField, BsonNull.Value);
            await _collection.UpdateOneAsync(filter, update);
            _softDeleteProp.SetValue(entity, null);
        }

        public Task<T> FindByIdIncludingDeletedAsync(TId id) =>
            FindByIdInternalAsync(id, includeDeleted: true);

        public Task<List<T>> FindAllIncludingDeletedAsync() => FindAllInternalAsync(includeDeleted: true);

        private BsonDocument ToBson(T entity)
        {
            var doc = ToBsonAny(entity, typeof(T));
            // Inclui DeletedAt se [SoftDelete] estiver ativo (não tem [Column]).
            if (_softDeleteField != null)
            {
                var raw = _softDeleteProp.GetValue(entity);
                doc[_softDeleteField] = raw == null ? BsonNull.Value : BsonValue.Create(raw);
            }
            return doc;
        }

        private static BsonDocument ToBsonAny(object entity, Type entityType)
        {
            if (entity == null) return null;
            var doc = new BsonDocument();
            foreach (var prop in TypeMetadataCache.GetProperties(entityType))
            {
                if (TypeMetadataCache.GetEmbeddedAttribute(prop) != null)
                {
                    var fieldName = ResolveEmbeddedFieldName(prop);
                    var value = prop.GetValue(entity);
                    doc[fieldName] = SerializeEmbedded(prop.PropertyType, value);
                    continue;
                }

                var col = TypeMetadataCache.GetColumnAttribute(prop);
                if (col == null) continue;
                var raw = prop.GetValue(entity);
                var name = col.IsPrimaryKey ? "_id" : col.Name;
                doc[name] = raw == null ? BsonNull.Value : BsonValue.Create(raw);
            }
            return doc;
        }

        private static BsonValue SerializeEmbedded(Type propertyType, object value)
        {
            if (value == null) return BsonNull.Value;

            var elementType = propertyType.GetElementType()
                              ?? (propertyType.IsGenericType ? propertyType.GetGenericArguments()[0] : null);

            if (elementType != null && value is System.Collections.IEnumerable enumerable)
            {
                var array = new BsonArray();
                foreach (var item in enumerable)
                    array.Add(item == null ? BsonNull.Value : (BsonValue)ToBsonAny(item, elementType));
                return array;
            }

            return ToBsonAny(value, propertyType);
        }

        private static string ResolveEmbeddedFieldName(PropertyInfo prop)
        {
            // Permite [Column("nome")] em conjunto com [Embedded] para customizar o
            // nome do campo. Caso contrário usa o nome da propriedade C#.
            var col = TypeMetadataCache.GetColumnAttribute(prop);
            return col?.Name ?? prop.Name;
        }

        private T FromBson(BsonDocument doc)
        {
            var instance = (T)FromBsonAny(doc, typeof(T));
            // Hidrata DeletedAt quando [SoftDelete] está ativo.
            if (_softDeleteField != null && doc.Contains(_softDeleteField))
            {
                var bson = doc[_softDeleteField];
                if (!bson.IsBsonNull)
                {
                    var dt = MongoDB.Bson.BsonTypeMapper.MapToDotNetValue(bson);
                    if (dt is DateTime parsed)
                        _softDeleteProp.SetValue(instance, (DateTime?)parsed);
                }
            }
            return instance;
        }

        private static object FromBsonAny(BsonDocument doc, Type targetType)
        {
            var instance = Activator.CreateInstance(targetType);
            foreach (var prop in TypeMetadataCache.GetProperties(targetType))
            {
                if (!prop.CanWrite) continue;

                if (TypeMetadataCache.GetEmbeddedAttribute(prop) != null)
                {
                    var fieldName = ResolveEmbeddedFieldName(prop);
                    if (!doc.Contains(fieldName) || doc[fieldName].IsBsonNull) continue;
                    prop.SetValue(instance, DeserializeEmbedded(prop.PropertyType, doc[fieldName]));
                    continue;
                }

                var col = TypeMetadataCache.GetColumnAttribute(prop);
                if (col == null) continue;
                var name = col.IsPrimaryKey ? "_id" : col.Name;
                if (!doc.Contains(name)) continue;
                var bson = doc[name];
                if (bson.IsBsonNull) continue;

                var underlying = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
                object value = BsonTypeMapper.MapToDotNetValue(bson);
                if (value != null && value.GetType() != underlying)
                {
                    if (underlying == typeof(Guid))
                        value = Guid.Parse(value.ToString());
                    else if (underlying == typeof(string))
                        value = value.ToString();
                    else
                        value = Convert.ChangeType(value, underlying);
                }
                prop.SetValue(instance, value);
            }
            return instance;
        }

        private static object DeserializeEmbedded(Type propertyType, BsonValue bson)
        {
            var elementType = propertyType.GetElementType()
                              ?? (propertyType.IsGenericType ? propertyType.GetGenericArguments()[0] : null);

            if (elementType != null && bson is BsonArray array)
            {
                var items = array
                    .Where(b => !b.IsBsonNull)
                    .Select(b => FromBsonAny(b.AsBsonDocument, elementType))
                    .ToArray();

                if (propertyType.IsArray)
                {
                    var result = Array.CreateInstance(elementType, items.Length);
                    for (int i = 0; i < items.Length; i++) result.SetValue(items[i], i);
                    return result;
                }

                // List<T> ou IEnumerable<T> — devolve List<T>.
                var listType = typeof(System.Collections.Generic.List<>).MakeGenericType(elementType);
                var list = (System.Collections.IList)Activator.CreateInstance(listType);
                foreach (var item in items) list.Add(item);
                return list;
            }

            if (bson is BsonDocument subDoc)
                return FromBsonAny(subDoc, propertyType);

            return null;
        }

        private static PropertyInfo ResolveIdProperty()
        {
            foreach (var prop in TypeMetadataCache.GetProperties(typeof(T)))
            {
                var col = TypeMetadataCache.GetColumnAttribute(prop);
                if (col != null && col.IsPrimaryKey) return prop;
            }
            throw new InvalidOperationException($"Type {typeof(T).Name} has no primary key column.");
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
