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

        public MongoRepository(IMongoDatabase database)
        {
            if (database == null) throw new ArgumentNullException(nameof(database));
            var tableName = new T().TableName;
            _collection = database.GetCollection<BsonDocument>(tableName);
            _idProp = ResolveIdProperty();
            _idColumnName = TypeMetadataCache.GetColumnAttribute(_idProp).Name;
        }

        public Task EnsureSchemaAsync() => Task.CompletedTask; // Mongo é schemaless.

        public async Task<System.Collections.Generic.IReadOnlyList<T>> SaveManyAsync(System.Collections.Generic.IEnumerable<T> entities)
        {
            if (entities == null) throw new ArgumentNullException(nameof(entities));
            var list = new System.Collections.Generic.List<T>();
            foreach (var entity in entities)
            {
                await SaveAsync(entity);
                list.Add(entity);
            }
            return list;
        }

        public async Task<T> SaveAsync(T entity)
        {
            var idValue = _idProp.GetValue(entity);
            var isNew = IsDefaultId(idValue);
            var now = DateTime.UtcNow;

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
                var doc = ToBson(entity);
                await _collection.InsertOneAsync(doc);
            }
            else
            {
                entity.UpdatedAt = now;
                var doc = ToBson(entity);
                var filter = Builders<BsonDocument>.Filter.Eq("_id", doc["_id"]);
                await _collection.ReplaceOneAsync(filter, doc, new ReplaceOptions { IsUpsert = true });
            }
            return entity;
        }

        public async Task<T> FindByIdAsync(TId id, bool includeRelated = false)
        {
            var filter = Builders<BsonDocument>.Filter.Eq("_id", BsonValue.Create(id));
            var doc = await _collection.Find(filter).FirstOrDefaultAsync();
            return doc == null ? null : FromBson(doc);
        }

        public async Task<List<T>> FindAllAsync(bool includeRelated = false)
        {
            var docs = await _collection.Find(FilterDefinition<BsonDocument>.Empty).ToListAsync();
            return docs.Select(FromBson).ToList();
        }

        public async Task DeleteAsync(T entity)
        {
            var idValue = _idProp.GetValue(entity);
            var filter = Builders<BsonDocument>.Filter.Eq("_id", BsonValue.Create(idValue));
            await _collection.DeleteOneAsync(filter);
        }

        private BsonDocument ToBson(T entity)
        {
            var doc = new BsonDocument();
            foreach (var prop in TypeMetadataCache.GetProperties(typeof(T)))
            {
                var col = TypeMetadataCache.GetColumnAttribute(prop);
                if (col == null) continue;
                var value = prop.GetValue(entity);
                var fieldName = col.IsPrimaryKey ? "_id" : col.Name;
                doc[fieldName] = value == null ? BsonNull.Value : BsonValue.Create(value);
            }
            return doc;
        }

        private T FromBson(BsonDocument doc)
        {
            var instance = new T();
            foreach (var prop in TypeMetadataCache.GetProperties(typeof(T)))
            {
                var col = TypeMetadataCache.GetColumnAttribute(prop);
                if (col == null || !prop.CanWrite) continue;
                var fieldName = col.IsPrimaryKey ? "_id" : col.Name;
                if (!doc.Contains(fieldName)) continue;
                var bson = doc[fieldName];
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
            if (t == typeof(Guid)) return (Guid)idValue == Guid.Empty;
            if (t == typeof(string)) return string.IsNullOrEmpty((string)idValue);
            return Equals(idValue, Activator.CreateInstance(t));
        }
    }
}
