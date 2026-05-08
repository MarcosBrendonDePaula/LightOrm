using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using LightOrm.Core.Attributes;
using LightOrm.Core.Models;
using LightOrm.Core.Utilities;

namespace LightOrm.Core.Sql
{
    /// <summary>
    /// Carrega navigation properties em uma ou várias instâncias raiz, usando
    /// IDialect (sem dependência de provider). Resolve N+1 do FindAll fazendo
    /// uma única query por relacionamento com WHERE fk IN (...).
    /// </summary>
    internal static class RelatedLoader
    {
        public static async Task LoadAsync(DbConnection connection, IDialect dialect,
            Type rootType, IList<object> instances)
        {
            if (instances == null || instances.Count == 0) return;

            var props = TypeMetadataCache.GetProperties(rootType);
            var oneToOne = props.Where(p => TypeMetadataCache.GetOneToOneAttribute(p) != null).ToList();
            var oneToMany = props.Where(p => TypeMetadataCache.GetOneToManyAttribute(p) != null).ToList();
            var manyToMany = props.Where(p => TypeMetadataCache.GetManyToManyAttribute(p) != null).ToList();

            foreach (var prop in oneToOne)
                await LoadOneToOneAsync(connection, dialect, rootType, instances, prop);

            foreach (var prop in oneToMany)
                await LoadOneToManyAsync(connection, dialect, rootType, instances, prop);

            foreach (var prop in manyToMany)
                await LoadManyToManyAsync(connection, dialect, rootType, instances, prop);
        }

        private static async Task LoadOneToOneAsync(DbConnection connection, IDialect dialect,
            Type rootType, IList<object> instances, PropertyInfo navProp)
        {
            var attr = TypeMetadataCache.GetOneToOneAttribute(navProp);
            var fkProp = rootType.GetProperty(attr.ForeignKeyProperty);
            if (fkProp == null) return;

            var fkValues = instances.Select(i => fkProp.GetValue(i))
                .Where(v => v != null && !IsZero(v))
                .Distinct()
                .ToList();
            if (fkValues.Count == 0) return;

            var relatedTable = ResolveTableName(attr.RelatedType);
            var relatedById = await LoadByPkInAsync(connection, dialect, attr.RelatedType, relatedTable, fkValues);

            foreach (var instance in instances)
            {
                var fk = fkProp.GetValue(instance);
                if (fk != null && relatedById.TryGetValue(fk, out var related))
                    navProp.SetValue(instance, related);
            }
        }

        private static async Task LoadOneToManyAsync(DbConnection connection, IDialect dialect,
            Type rootType, IList<object> instances, PropertyInfo navProp)
        {
            var attr = TypeMetadataCache.GetOneToManyAttribute(navProp);
            var rootPk = GetPkProperty(rootType);
            var rootIds = instances.Select(i => rootPk.GetValue(i))
                .Where(v => v != null && !IsZero(v))
                .Distinct()
                .ToList();
            if (rootIds.Count == 0) return;

            var relatedTable = ResolveTableName(attr.RelatedType);
            var fkColumnName = attr.ForeignKeyProperty;
            // Aceita atributo apontando para nome da coluna ou da propriedade.
            var relatedFkProp = ResolveColumnProperty(attr.RelatedType, fkColumnName);
            if (relatedFkProp != null)
                fkColumnName = TypeMetadataCache.GetColumnAttribute(relatedFkProp).Name;

            var relatedItems = await LoadByColumnInAsync(connection, dialect, attr.RelatedType,
                relatedTable, fkColumnName, rootIds);

            // Agrupar por valor da FK e atribuir como array no navigation property.
            var grouped = new Dictionary<object, List<object>>();
            foreach (var (fkValue, item) in relatedItems)
            {
                if (!grouped.TryGetValue(fkValue, out var list))
                    grouped[fkValue] = list = new List<object>();
                list.Add(item);
            }

            var elementType = navProp.PropertyType.GetElementType()
                              ?? navProp.PropertyType.GetGenericArguments().FirstOrDefault();
            if (elementType == null) return;

            foreach (var instance in instances)
            {
                var rootId = rootPk.GetValue(instance);
                grouped.TryGetValue(rootId, out var items);
                items ??= new List<object>();
                var array = Array.CreateInstance(elementType, items.Count);
                for (int i = 0; i < items.Count; i++) array.SetValue(items[i], i);
                if (navProp.CanWrite) navProp.SetValue(instance, array);
            }
        }

        private static async Task LoadManyToManyAsync(DbConnection connection, IDialect dialect,
            Type rootType, IList<object> instances, PropertyInfo navProp)
        {
            var attr = TypeMetadataCache.GetManyToManyAttribute(navProp);
            var rootPk = GetPkProperty(rootType);
            var rootIds = instances.Select(i => rootPk.GetValue(i))
                .Where(v => v != null && !IsZero(v))
                .Distinct()
                .ToList();
            if (rootIds.Count == 0) return;

            var relatedTable = ResolveTableName(attr.RelatedType);
            var assoc = dialect.QuoteIdentifier(attr.AssociationTable);
            var related = dialect.QuoteIdentifier(relatedTable);
            var sourceCol = dialect.QuoteIdentifier(attr.SourceForeignKey);
            var targetCol = dialect.QuoteIdentifier(attr.TargetForeignKey);
            var relatedPkCol = dialect.QuoteIdentifier(GetPkColumnName(attr.RelatedType));

            var (placeholders, addValues) = BuildInClause(dialect, "src", rootIds);
            var sql = $"SELECT a.{sourceCol} AS __src, r.* FROM {assoc} a " +
                      $"JOIN {related} r ON r.{relatedPkCol} = a.{targetCol} " +
                      $"WHERE a.{sourceCol} IN ({placeholders})";

            using var cmd = dialect.CreateCommand(connection);
            cmd.CommandText = sql;
            addValues(cmd);

            var grouped = new Dictionary<object, List<object>>();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var srcOrdinal = reader.GetOrdinal("__src");
                var srcValue = dialect.FromDbValue(reader.GetValue(srcOrdinal), rootPk.PropertyType);
                var item = Activator.CreateInstance(attr.RelatedType);
                PopulateInstance(reader, item, attr.RelatedType, dialect);
                if (!grouped.TryGetValue(srcValue, out var list))
                    grouped[srcValue] = list = new List<object>();
                list.Add(item);
            }

            var elementType = navProp.PropertyType.GetElementType()
                              ?? navProp.PropertyType.GetGenericArguments().FirstOrDefault();
            if (elementType == null) return;

            foreach (var instance in instances)
            {
                var rootId = rootPk.GetValue(instance);
                grouped.TryGetValue(rootId, out var items);
                items ??= new List<object>();
                var array = Array.CreateInstance(elementType, items.Count);
                for (int i = 0; i < items.Count; i++) array.SetValue(items[i], i);
                if (navProp.CanWrite) navProp.SetValue(instance, array);
            }
        }

        private static async Task<Dictionary<object, object>> LoadByPkInAsync(
            DbConnection connection, IDialect dialect, Type relatedType,
            string relatedTable, IList<object> ids)
        {
            var pk = GetPkProperty(relatedType);
            var pkCol = TypeMetadataCache.GetColumnAttribute(pk).Name;
            var (placeholders, addValues) = BuildInClause(dialect, "id", ids);

            var sql = $"SELECT * FROM {dialect.QuoteIdentifier(relatedTable)} " +
                      $"WHERE {dialect.QuoteIdentifier(pkCol)} IN ({placeholders})";

            using var cmd = dialect.CreateCommand(connection);
            cmd.CommandText = sql;
            addValues(cmd);

            var result = new Dictionary<object, object>();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var instance = Activator.CreateInstance(relatedType);
                PopulateInstance(reader, instance, relatedType, dialect);
                var pkValue = pk.GetValue(instance);
                if (pkValue != null) result[pkValue] = instance;
            }
            return result;
        }

        private static async Task<List<(object fkValue, object item)>> LoadByColumnInAsync(
            DbConnection connection, IDialect dialect, Type relatedType,
            string relatedTable, string columnName, IList<object> values)
        {
            var (placeholders, addValues) = BuildInClause(dialect, "v", values);
            var sql = $"SELECT * FROM {dialect.QuoteIdentifier(relatedTable)} " +
                      $"WHERE {dialect.QuoteIdentifier(columnName)} IN ({placeholders})";

            using var cmd = dialect.CreateCommand(connection);
            cmd.CommandText = sql;
            addValues(cmd);

            var result = new List<(object, object)>();
            using var reader = await cmd.ExecuteReaderAsync();
            var fkOrdinal = -1;
            while (await reader.ReadAsync())
            {
                if (fkOrdinal < 0) fkOrdinal = reader.GetOrdinal(columnName);
                var instance = Activator.CreateInstance(relatedType);
                PopulateInstance(reader, instance, relatedType, dialect);
                var fkProp = ResolveColumnProperty(relatedType, columnName);
                var fkValue = fkProp != null
                    ? fkProp.GetValue(instance)
                    : dialect.FromDbValue(reader.GetValue(fkOrdinal), typeof(object));
                result.Add((fkValue, instance));
            }
            return result;
        }

        private static void PopulateInstance(DbDataReader reader, object instance, Type type, IDialect dialect)
        {
            foreach (var prop in TypeMetadataCache.GetProperties(type))
            {
                var col = TypeMetadataCache.GetColumnAttribute(prop);
                if (col == null || !prop.CanWrite) continue;
                int ordinal;
                try { ordinal = reader.GetOrdinal(col.Name); }
                catch (IndexOutOfRangeException) { continue; }
                if (reader.IsDBNull(ordinal)) continue;
                var raw = reader.GetValue(ordinal);
                var converted = dialect.FromDbValue(raw, prop.PropertyType);
                prop.SetValue(instance, converted);
            }
        }

        private static (string placeholders, Action<DbCommand> addValues) BuildInClause(
            IDialect dialect, string prefix, IList<object> values)
        {
            var names = new List<string>(values.Count);
            for (int i = 0; i < values.Count; i++)
                names.Add($"{dialect.ParameterPrefix}{prefix}{i}");

            void AddValues(DbCommand cmd)
            {
                for (int i = 0; i < values.Count; i++)
                {
                    var p = cmd.CreateParameter();
                    p.ParameterName = $"{dialect.ParameterPrefix}{prefix}{i}";
                    p.Value = dialect.ToDbValue(values[i], values[i]?.GetType() ?? typeof(object)) ?? DBNull.Value;
                    cmd.Parameters.Add(p);
                }
            }

            return (string.Join(", ", names), AddValues);
        }

        private static string ResolveTableName(Type modelType)
        {
            var instance = Activator.CreateInstance(modelType) as IModel;
            if (instance == null)
                throw new InvalidOperationException($"Tipo {modelType.Name} não implementa IModel.");
            return instance.GetTableName();
        }

        private static PropertyInfo GetPkProperty(Type type)
        {
            foreach (var p in TypeMetadataCache.GetProperties(type))
            {
                var col = TypeMetadataCache.GetColumnAttribute(p);
                if (col != null && col.IsPrimaryKey) return p;
            }
            throw new InvalidOperationException($"Tipo {type.Name} não tem chave primária.");
        }

        private static string GetPkColumnName(Type type) =>
            TypeMetadataCache.GetColumnAttribute(GetPkProperty(type)).Name;

        private static PropertyInfo ResolveColumnProperty(Type type, string nameOrColumn)
        {
            foreach (var p in TypeMetadataCache.GetProperties(type))
            {
                if (p.Name == nameOrColumn) return p;
                var col = TypeMetadataCache.GetColumnAttribute(p);
                if (col != null && col.Name == nameOrColumn) return p;
            }
            return null;
        }

        private static bool IsZero(object value)
        {
            if (value == null) return true;
            var t = value.GetType();
            if (t == typeof(int)) return (int)value == 0;
            if (t == typeof(long)) return (long)value == 0;
            if (t == typeof(short)) return (short)value == 0;
            if (t == typeof(Guid)) return (Guid)value == Guid.Empty;
            if (t == typeof(string)) return string.IsNullOrEmpty((string)value);
            return false;
        }
    }
}
