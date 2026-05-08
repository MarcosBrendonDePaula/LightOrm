using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using LightOrm.Core.Attributes;
using LightOrm.Core.Models;
using LightOrm.Core.Utilities;
using MongoDB.Bson;
using MongoDB.Driver;

namespace LightOrm.Mongo
{
    /// <summary>
    /// Builder fluente para Mongo, espelhando a API de SqlQuery via IQuery.
    /// Filtros e ordenação operam contra BsonDocument; o operador "LIKE" é
    /// traduzido para regex equivalente (escape de metacharacters + % → .*).
    /// </summary>
    public class MongoQuery<T, TId> : IQuery<T, TId> where T : BaseModel<T, TId>, new()
    {
        private readonly IMongoCollection<BsonDocument> _collection;
        private readonly Func<BsonDocument, T> _hydrate;
        private readonly List<FilterDefinition<BsonDocument>> _filters = new List<FilterDefinition<BsonDocument>>();
        private readonly List<(string field, bool desc)> _sort = new List<(string, bool)>();
        private int? _limit;
        private int? _skip;

        internal MongoQuery(IMongoCollection<BsonDocument> collection, Func<BsonDocument, T> hydrate)
        {
            _collection = collection;
            _hydrate = hydrate;
        }

        public IQuery<T, TId> Where(string propertyName, string op, object value)
        {
            var field = ResolveFieldName(propertyName);
            var filter = BuildFilter(field, op, value);
            _filters.Add(filter);
            return this;
        }

        public IQuery<T, TId> Where(string propertyName, object value) =>
            Where(propertyName, "=", value);

        public IQuery<T, TId> WhereIn(string propertyName, IEnumerable<object> values)
        {
            var field = ResolveFieldName(propertyName);
            var list = values?.Select(v => v == null ? BsonNull.Value : BsonValue.Create(v)).ToList()
                       ?? new List<BsonValue>();
            if (list.Count == 0)
            {
                // IN vazio: filtro impossível.
                _filters.Add(Builders<BsonDocument>.Filter.Eq("__never", "__never"));
                return this;
            }
            _filters.Add(Builders<BsonDocument>.Filter.In(field, list));
            return this;
        }

        public IQuery<T, TId> OrderBy(string propertyName)
        {
            _sort.Add((ResolveFieldName(propertyName), false));
            return this;
        }

        public IQuery<T, TId> OrderByDescending(string propertyName)
        {
            _sort.Add((ResolveFieldName(propertyName), true));
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
            _skip = offset;
            return this;
        }

        public async Task<List<T>> ToListAsync()
        {
            var find = _collection.Find(BuildFilter());
            ApplySort(find);
            if (_skip.HasValue) find = find.Skip(_skip.Value);
            if (_limit.HasValue) find = find.Limit(_limit.Value);
            var docs = await find.ToListAsync();
            return docs.Select(_hydrate).ToList();
        }

        public async Task<T> FirstOrDefaultAsync()
        {
            _limit = 1;
            var list = await ToListAsync();
            return list.Count == 0 ? null : list[0];
        }

        public async Task<int> CountAsync()
        {
            var count = await _collection.CountDocumentsAsync(BuildFilter());
            return (int)Math.Min(count, int.MaxValue);
        }

        public async Task<bool> AnyAsync()
        {
            var count = await _collection.Find(BuildFilter()).Limit(1).CountDocumentsAsync();
            return count > 0;
        }

        // ---------- internals ----------

        private FilterDefinition<BsonDocument> BuildFilter()
        {
            if (_filters.Count == 0) return FilterDefinition<BsonDocument>.Empty;
            return Builders<BsonDocument>.Filter.And(_filters);
        }

        private void ApplySort(IFindFluent<BsonDocument, BsonDocument> find)
        {
            if (_sort.Count == 0) return;
            var sortDef = _sort[0].desc
                ? Builders<BsonDocument>.Sort.Descending(_sort[0].field)
                : Builders<BsonDocument>.Sort.Ascending(_sort[0].field);
            for (int i = 1; i < _sort.Count; i++)
            {
                var s = _sort[i];
                sortDef = s.desc
                    ? sortDef.Descending(s.field)
                    : sortDef.Ascending(s.field);
            }
            find.Sort(sortDef);
        }

        private static FilterDefinition<BsonDocument> BuildFilter(string field, string op, object value)
        {
            var b = Builders<BsonDocument>.Filter;
            var bson = value == null ? BsonNull.Value : BsonValue.Create(value);

            switch (op?.ToUpperInvariant())
            {
                case "=":        return b.Eq(field, bson);
                case "!=":
                case "<>":       return b.Ne(field, bson);
                case "<":        return b.Lt(field, bson);
                case "<=":       return b.Lte(field, bson);
                case ">":        return b.Gt(field, bson);
                case ">=":       return b.Gte(field, bson);
                case "LIKE":     return b.Regex(field, LikeToRegex(value?.ToString() ?? ""));
                case "NOT LIKE": return b.Not(b.Regex(field, LikeToRegex(value?.ToString() ?? "")));
                default:
                    throw new ArgumentException($"Operador '{op}' não suportado em MongoQuery.", nameof(op));
            }
        }

        private static BsonRegularExpression LikeToRegex(string pattern)
        {
            // Escapa metacharacters de regex e converte LIKE wildcards: % → .*, _ → .
            var sb = new System.Text.StringBuilder("^");
            foreach (var c in pattern)
            {
                if (c == '%') sb.Append(".*");
                else if (c == '_') sb.Append('.');
                else sb.Append(Regex.Escape(c.ToString()));
            }
            sb.Append('$');
            return new BsonRegularExpression(sb.ToString());
        }

        private static string ResolveFieldName(string propertyOrColumnName)
        {
            if (string.IsNullOrEmpty(propertyOrColumnName))
                throw new ArgumentException("Nome da propriedade não pode ser vazio.", nameof(propertyOrColumnName));

            foreach (var prop in TypeMetadataCache.GetProperties(typeof(T)))
            {
                var col = TypeMetadataCache.GetColumnAttribute(prop);
                if (col == null) continue;
                if (prop.Name == propertyOrColumnName)
                    return col.IsPrimaryKey ? "_id" : col.Name;
                if (col.Name == propertyOrColumnName)
                    return col.IsPrimaryKey ? "_id" : col.Name;
            }
            throw new ArgumentException(
                $"Propriedade ou coluna '{propertyOrColumnName}' não encontrada em {typeof(T).Name}.",
                nameof(propertyOrColumnName));
        }
    }
}
