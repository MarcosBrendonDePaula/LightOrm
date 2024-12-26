using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;
using LightOrm.Core.Attributes;

namespace LightOrm.Core.Models
{
    public interface IModel
    {
        string GetTableName();
        Task EnsureTableExistsAsync(MySqlConnection connection);
    }

    public abstract class BaseModel<T> : IModel where T : BaseModel<T>, new()
    {
        public abstract string TableName { get; }

        [Column("Id", isPrimaryKey: true, autoIncrement: true)]
        public int Id { get; set; }

        [Column("CreatedAt", isNullable: false)]
        public DateTime CreatedAt { get; set; }

        [Column("UpdatedAt", isNullable: false)]
        public DateTime UpdatedAt { get; set; }

        [Column("__hash_v", length: 64)]
        public virtual string HashVersion { get; set; }

        private static readonly Dictionary<string, (string Hash, object Data)> _cache = new Dictionary<string, (string Hash, object Data)>();

        private string GenerateHash()
        {
            var properties = GetType().GetProperties()
                .Where(p => p.GetCustomAttribute<ColumnAttribute>() != null 
                    && p.Name != nameof(HashVersion));

            var values = properties.Select(p => p.GetValue(this)?.ToString() ?? "null");
            var data = string.Join("|", values);
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var bytes = System.Text.Encoding.UTF8.GetBytes(data);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }

        private string GetCacheKey() => $"{TableName}_{Id}";

        private void UpdateHash()
        {
            HashVersion = GenerateHash();
        }

        private static void UpdateCache(string key, T instance)
        {
            _cache[key] = (instance.HashVersion, instance);
        }

        private static T GetFromCache(string key)
        {
            if (_cache.TryGetValue(key, out var cached))
            {
                return cached.Data as T;
            }
            return null;
        }

        private static bool IsCacheValid(string key, string hash)
        {
            return _cache.TryGetValue(key, out var cached) && cached.Hash == hash;
        }

        public string GetTableName() => TableName;

        private static async Task LoadRelatedEntityAsync(MySqlConnection connection, T instance, PropertyInfo property, Type relatedType, object foreignKeyValue)
        {
            var relatedTableName = GetTableNameFromType(relatedType);
            string query = $"SELECT * FROM {relatedTableName} WHERE Id = @Id";

            using var cmd = new MySqlCommand(query, connection);
            cmd.Parameters.AddWithValue("@Id", foreignKeyValue);

            using var reader = await cmd.ExecuteReaderAsync() as MySqlDataReader;
            if (await reader.ReadAsync())
            {
                var relatedInstance = Activator.CreateInstance(relatedType);
                foreach (var relatedProperty in relatedType.GetProperties())
                {
                    var relatedColumnAttr = relatedProperty.GetCustomAttribute<ColumnAttribute>();
                    if (relatedColumnAttr != null)
                    {
                        string columnName = relatedColumnAttr.Name;
                        if (!reader.IsDBNull(reader.GetOrdinal(columnName)))
                        {
                            object value = reader[columnName];
                            if (relatedProperty.CanWrite)
                            {
                                object convertedValue;
                                var targetType = Nullable.GetUnderlyingType(relatedProperty.PropertyType) ?? relatedProperty.PropertyType;

                                // Special handling for DateTime values from MySQL
                                if (targetType == typeof(DateTime))
                                {
                                    if (value is DateTime dateValue)
                                    {
                                        // Convert MySQL timestamp to local time
                                        convertedValue = DateTime.SpecifyKind(dateValue, DateTimeKind.Local);
                                    }
                                    else if (value is MySql.Data.Types.MySqlDateTime mySqlDateTime)
                                    {
                                        // Handle MySqlDateTime type
                                        convertedValue = new DateTime(
                                            mySqlDateTime.Year, mySqlDateTime.Month, mySqlDateTime.Day,
                                            mySqlDateTime.Hour, mySqlDateTime.Minute, mySqlDateTime.Second,
                                            DateTimeKind.Local);
                                    }
                                    else
                                    {
                                        // Try standard conversion as fallback
                                        convertedValue = Convert.ChangeType(value, targetType);
                                    }
                                }
                                else
                                {
                                    convertedValue = Convert.ChangeType(value, targetType);
                                }
                                relatedProperty.SetValue(relatedInstance, convertedValue);
                            }
                        }
                    }
                }
                property.SetValue(instance, relatedInstance);
            }
        }

        private static async Task LoadRelatedDataAsync(MySqlConnection connection, T instance)
        {
            var properties = typeof(T).GetProperties();
            var manyToManyProps = properties.Where(p => p.GetCustomAttribute<ManyToManyAttribute>() != null).ToList();
            var oneToManyProps = properties.Where(p => p.GetCustomAttribute<OneToManyAttribute>() != null).ToList();
            var oneToOneProps = properties.Where(p => p.GetCustomAttribute<OneToOneAttribute>() != null).ToList();
            var foreignKeyProps = properties.Where(p => p.GetCustomAttribute<ForeignKeyAttribute>() != null).ToList();

            // Build optimized queries for each relationship type
            if (manyToManyProps.Any())
            {
                await LoadManyToManyRelationshipsAsync(connection, instance, manyToManyProps);
            }

            if (oneToManyProps.Any())
            {
                await LoadOneToManyRelationshipsAsync(connection, instance, oneToManyProps);
            }

            if (oneToOneProps.Any() || foreignKeyProps.Any())
            {
                await LoadOneToOneRelationshipsAsync(connection, instance, oneToOneProps, foreignKeyProps);
            }
        }

        private static async Task LoadManyToManyRelationshipsAsync(MySqlConnection connection, T instance, IEnumerable<PropertyInfo> properties)
        {
            foreach (var property in properties)
            {
                var attr = property.GetCustomAttribute<ManyToManyAttribute>();
                var relatedType = attr.RelatedType;
                
                // Optimized query with JOIN
                string query = $@"
                    SELECT r.*, a.*
                    FROM {attr.AssociationTable} a
                    JOIN {GetTableNameFromType(relatedType)} r 
                        ON r.Id = a.{attr.TargetForeignKey}
                    WHERE a.{attr.SourceForeignKey} = @Id";

                using var cmd = new MySqlCommand(query, connection);
                cmd.Parameters.AddWithValue("@Id", instance.Id);

                var relatedItems = new List<object>();
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var relatedInstance = Activator.CreateInstance(relatedType);
                    PopulateInstance(reader, relatedInstance as IModel);
                    relatedItems.Add(relatedInstance);
                }

                if (property.CanWrite)
                {
                    var arrayType = property.PropertyType;
                    var array = Array.CreateInstance(arrayType.GetElementType(), relatedItems.Count);
                    for (int i = 0; i < relatedItems.Count; i++)
                    {
                        array.SetValue(relatedItems[i], i);
                    }
                    property.SetValue(instance, array);
                }
            }
        }

        private static async Task LoadOneToManyRelationshipsAsync(MySqlConnection connection, T instance, IEnumerable<PropertyInfo> properties)
        {
            foreach (var property in properties)
            {
                var attr = property.GetCustomAttribute<OneToManyAttribute>();
                var relatedType = attr.RelatedType;

                // Optimized query
                string query = $@"
                    SELECT *
                    FROM {GetTableNameFromType(relatedType)}
                    WHERE {attr.ForeignKeyProperty} = @Id";

                using var cmd = new MySqlCommand(query, connection);
                cmd.Parameters.AddWithValue("@Id", instance.Id);

                var relatedItems = new List<object>();
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var relatedInstance = Activator.CreateInstance(relatedType);
                    PopulateInstance(reader, relatedInstance as IModel);
                    relatedItems.Add(relatedInstance);
                }

                if (property.CanWrite)
                {
                    var arrayType = property.PropertyType;
                    var array = Array.CreateInstance(arrayType.GetElementType(), relatedItems.Count);
                    for (int i = 0; i < relatedItems.Count; i++)
                    {
                        array.SetValue(relatedItems[i], i);
                    }
                    property.SetValue(instance, array);
                }
            }
        }

        private static async Task LoadOneToOneRelationshipsAsync(MySqlConnection connection, T instance, IEnumerable<PropertyInfo> oneToOneProps, IEnumerable<PropertyInfo> foreignKeyProps)
        {
            // Build a single query for all one-to-one relationships
            var queries = new List<string>();
            var parameters = new Dictionary<string, object>();

            foreach (var property in oneToOneProps)
            {
                var attr = property.GetCustomAttribute<OneToOneAttribute>();
                var foreignKeyProperty = typeof(T).GetProperty(attr.ForeignKeyProperty);
                if (foreignKeyProperty != null)
                {
                    var foreignKeyValue = foreignKeyProperty.GetValue(instance);
                    if (foreignKeyValue != null)
                    {
                        var paramName = $"@Id_{property.Name}";
                        queries.Add($@"
                            SELECT *
                            FROM {GetTableNameFromType(attr.RelatedType)}
                            WHERE Id = {paramName}");
                        parameters.Add(paramName, foreignKeyValue);
                    }
                }
            }

            foreach (var property in foreignKeyProps)
            {
                var attr = property.GetCustomAttribute<ForeignKeyAttribute>();
                var navigationPropertyName = property.Name.Replace("Id", "");
                var navigationProperty = typeof(T).GetProperty(navigationPropertyName);
                if (navigationProperty != null)
                {
                    var foreignKeyValue = property.GetValue(instance);
                    if (foreignKeyValue != null)
                    {
                        var paramName = $"@Id_{property.Name}";
                        queries.Add($@"
                            SELECT *
                            FROM {attr.ReferenceTable}
                            WHERE Id = {paramName}");
                        parameters.Add(paramName, foreignKeyValue);
                    }
                }
            }

            if (queries.Count > 0)
            {
                string combinedQuery = string.Join(";\n", queries);
                using var cmd = new MySqlCommand(combinedQuery, connection);
                foreach (var param in parameters)
                {
                    cmd.Parameters.AddWithValue(param.Key, param.Value);
                }

                using var reader = await cmd.ExecuteReaderAsync();
                int queryIndex = 0;

                do
                {
                    if (await reader.ReadAsync())
                    {
                        if (queryIndex < oneToOneProps.Count())
                        {
                            var property = oneToOneProps.ElementAt(queryIndex);
                            var attr = property.GetCustomAttribute<OneToOneAttribute>();
                            var relatedInstance = Activator.CreateInstance(attr.RelatedType);
                            PopulateInstance(reader, relatedInstance as IModel);
                            property.SetValue(instance, relatedInstance);
                        }
                        else
                        {
                            var property = foreignKeyProps.ElementAt(queryIndex - oneToOneProps.Count());
                            var navigationPropertyName = property.Name.Replace("Id", "");
                            var navigationProperty = typeof(T).GetProperty(navigationPropertyName);
                            var attr = property.GetCustomAttribute<ForeignKeyAttribute>();
                            var relatedType = GetModelTypeByTableName(attr.ReferenceTable);
                            if (relatedType != null && navigationProperty != null)
                            {
                                var relatedInstance = Activator.CreateInstance(relatedType);
                                PopulateInstance(reader, relatedInstance as IModel);
                                navigationProperty.SetValue(instance, relatedInstance);
                            }
                        }
                    }
                    queryIndex++;
                } while (await reader.NextResultAsync());
            }
        }

        private static string GetTableNameFromType(Type type)
        {
            var instance = Activator.CreateInstance(type) as IModel;
            return instance?.GetTableName() ?? throw new InvalidOperationException($"Could not get table name for type {type.Name}");
        }

        private static Type GetModelTypeByTableName(string tableName)
        {
            var assembly = Assembly.GetExecutingAssembly();
            foreach (var type in assembly.GetTypes())
            {
                if (typeof(IModel).IsAssignableFrom(type) && !type.IsAbstract)
                {
                    var instance = Activator.CreateInstance(type) as IModel;
                    if (instance?.GetTableName() == tableName)
                    {
                        return type;
                    }
                }
            }
            return null;
        }

        protected static void PopulateInstance(System.Data.Common.DbDataReader reader, IModel instance)
        {
            foreach (var property in instance.GetType().GetProperties())
            {
                var columnAttr = property.GetCustomAttribute<ColumnAttribute>();
                if (columnAttr != null)
                {
                    string columnName = columnAttr.Name;
                    if (!reader.IsDBNull(reader.GetOrdinal(columnName)))
                    {
                        object value = reader[columnName];
                        if (property.CanWrite)
                        {
                            object convertedValue;
                            var targetType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

                            // Special handling for DateTime values from MySQL
                            if (targetType == typeof(DateTime))
                            {
                                if (value is DateTime dateValue)
                                {
                                    // Convert MySQL timestamp to local time
                                    convertedValue = DateTime.SpecifyKind(dateValue, DateTimeKind.Local);
                                }
                                else if (value is MySql.Data.Types.MySqlDateTime mySqlDateTime)
                                {
                                    // Handle MySqlDateTime type
                                    convertedValue = new DateTime(
                                        mySqlDateTime.Year, mySqlDateTime.Month, mySqlDateTime.Day,
                                        mySqlDateTime.Hour, mySqlDateTime.Minute, mySqlDateTime.Second,
                                        DateTimeKind.Local);
                                }
                                else
                                {
                                    // Try standard conversion as fallback
                                    convertedValue = Convert.ChangeType(value, targetType);
                                }
                            }
                            else
                            {
                                convertedValue = Convert.ChangeType(value, targetType);
                            }
                            property.SetValue(instance, convertedValue);
                        }
                    }
                }
            }
        }

        private string GenerateInsertSql()
        {
            var columnNames = new List<string>();
            var values = new List<string>();

            foreach (var property in typeof(T).GetProperties())
            {
                var columnAttr = property.GetCustomAttribute<ColumnAttribute>();
                if (columnAttr != null && !columnAttr.AutoIncrement && 
                    property.Name != nameof(CreatedAt) && property.Name != nameof(UpdatedAt))
                {
                    columnNames.Add(columnAttr.Name);
                    values.Add($"@{columnAttr.Name}");
                }
            }

            // Add timestamp columns
            columnNames.Add("CreatedAt");
            columnNames.Add("UpdatedAt");
            values.Add("CURRENT_TIMESTAMP");
            values.Add("CURRENT_TIMESTAMP");

            return $@"
                INSERT INTO {TableName} ({string.Join(", ", columnNames)}) 
                VALUES ({string.Join(", ", values)});
                SELECT LAST_INSERT_ID();";
        }

        private (string setClause, object primaryKeyValue) GetUpdateClauseAndPrimaryKeyValue(string keyColumn)
        {
            var setClauseList = new List<string>();
            object primaryKeyValue = null;

            foreach (var property in typeof(T).GetProperties())
            {
                var columnAttr = property.GetCustomAttribute<ColumnAttribute>();
                if (columnAttr != null)
                {
                    if (columnAttr.IsPrimaryKey)
                    {
                        primaryKeyValue = property.GetValue(this);
                    }
                    else if (!columnAttr.AutoIncrement && 
                            property.Name != nameof(CreatedAt) && 
                            property.Name != nameof(UpdatedAt))
                    {
                        setClauseList.Add($"{columnAttr.Name} = @{columnAttr.Name}");
                    }
                }
            }

            // Add UpdatedAt
            setClauseList.Add("UpdatedAt = CURRENT_TIMESTAMP");

            return (string.Join(", ", setClauseList), primaryKeyValue);
        }

        private void PopulateCommandParameters(MySqlCommand cmd)
        {
            foreach (var property in typeof(T).GetProperties())
            {
                var columnAttr = property.GetCustomAttribute<ColumnAttribute>();
                if (columnAttr != null && !columnAttr.AutoIncrement && 
                    property.Name != nameof(CreatedAt) && property.Name != nameof(UpdatedAt))
                {
                    string parameterName = $"@{columnAttr.Name}";
                    if (!cmd.Parameters.Contains(parameterName))
                    {
                        object value = property.GetValue(this);
                        if (value == null && columnAttr.DefaultValue != null)
                        {
                            value = columnAttr.DefaultValue;
                        }
                        cmd.Parameters.AddWithValue(parameterName, value ?? DBNull.Value);
                    }
                }
            }
        }

        public async Task EnsureTableExistsAsync(MySqlConnection connection)
        {
            if (connection.State != System.Data.ConnectionState.Open)
                await connection.OpenAsync();
                
            using var transaction = await connection.BeginTransactionAsync();

            try
            {
                var sql = GenerateCreateTableSql();
                using var cmd = new MySqlCommand(sql, connection);
                cmd.Transaction = transaction;
                await cmd.ExecuteNonQueryAsync();
                await transaction.CommitAsync();
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        private string GenerateCreateTableSql()
        {
            var columns = new List<string>();
            var constraints = new List<string>();

            // Add CreatedAt and UpdatedAt first with special handling
            columns.Add("CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP");
            columns.Add("UpdatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP");

            foreach (var property in typeof(T).GetProperties())
            {
                var columnAttr = property.GetCustomAttribute<ColumnAttribute>();
                if (columnAttr != null && property.Name != nameof(CreatedAt) && property.Name != nameof(UpdatedAt))
                {
                    string sqlType = GetSqlType(property.PropertyType, columnAttr);
                    string columnDef = $"{columnAttr.Name} {sqlType}";

                    if (columnAttr.IsPrimaryKey)
                        columnDef += " PRIMARY KEY";
                    if (columnAttr.AutoIncrement)
                        columnDef += " AUTO_INCREMENT";

                    columns.Add(columnDef);

                    // Add unique constraint
                    if (columnAttr.IsUnique)
                    {
                        constraints.Add($"UNIQUE KEY uk_{columnAttr.Name} ({columnAttr.Name})");
                    }

                    // Add index
                    if (columnAttr.HasIndex)
                    {
                        constraints.Add($"INDEX idx_{columnAttr.Name} ({columnAttr.Name})");
                    }

                    // Add check constraint
                    if (!string.IsNullOrEmpty(columnAttr.CheckConstraint))
                    {
                        constraints.Add($"CHECK ({columnAttr.CheckConstraint})");
                    }
                    else if (columnAttr.EnumValues?.Length > 0)
                    {
                        var values = string.Join("','", columnAttr.EnumValues);
                        constraints.Add($"CHECK ({columnAttr.Name} IN ('{values}'))");
                    }

                    // Add foreign key
                    var foreignKeyAttr = property.GetCustomAttribute<ForeignKeyAttribute>();
                    if (foreignKeyAttr != null)
                    {
                        constraints.Add($"FOREIGN KEY ({columnAttr.Name}) REFERENCES {foreignKeyAttr.ReferenceTable}({foreignKeyAttr.ReferenceColumn})");
                    }
                }
            }

            string sql = $@"
                CREATE TABLE IF NOT EXISTS {TableName} (
                    {string.Join(",\n    ", columns)}
                    {(constraints.Any() ? ",\n    " + string.Join(",\n    ", constraints) : "")}
                );";

            return sql;
        }

        private string GetSqlType(Type type, ColumnAttribute columnAttr)
        {
            // Handle nullable types
            Type underlyingType = Nullable.GetUnderlyingType(type) ?? type;

            string sqlType = underlyingType switch
            {
                Type t when t == typeof(int) => columnAttr.IsUnsigned ? "INT UNSIGNED" : "INT",
                Type t when t == typeof(long) => columnAttr.IsUnsigned ? "BIGINT UNSIGNED" : "BIGINT",
                Type t when t == typeof(string) => $"VARCHAR({(columnAttr.Length > 0 ? columnAttr.Length : 255)})",
                Type t when t == typeof(bool) => "BOOLEAN",
                Type t when t == typeof(DateTime) => "DATETIME",
                Type t when t == typeof(decimal) => $"DECIMAL({columnAttr.Precision},{columnAttr.Scale})",
                Type t when t == typeof(float) => "FLOAT",
                Type t when t == typeof(double) => "DOUBLE",
                _ => throw new NotSupportedException($"Type {type.Name} is not supported for SQL mapping.")
            };

            // Handle nullability
            if (columnAttr.IsNullable || Nullable.GetUnderlyingType(type) != null)
            {
                sqlType += " NULL";
            }
            else
            {
                sqlType += " NOT NULL";
            }

            // Handle default value
            if (columnAttr.DefaultValue != null)
            {
                string defaultValueStr = columnAttr.DefaultValue switch
                {
                    string s when s == "CURRENT_TIMESTAMP" => "CURRENT_TIMESTAMP",
                    string s => $"'{s}'",
                    DateTime dt => $"'{dt:yyyy-MM-dd HH:mm:ss}'",
                    bool b => b ? "1" : "0",
                    decimal d => d.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    float f => f.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    double d => d.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    _ => columnAttr.DefaultValue.ToString()
                };
                sqlType += $" DEFAULT {defaultValueStr}";
            }
            else if (!columnAttr.IsNullable && underlyingType == typeof(string))
            {
                sqlType += " DEFAULT ''";
            }

            return sqlType;
        }

        public async Task<T> SaveAsync(MySqlConnection connection)
        {
            if (Id == 0)
            {
                return await InsertAsync(connection);
            }
            else
            {
                return await UpdateAsync(connection);
            }
        }

        public static async Task<T> FindByIdAsync(MySqlConnection connection, int id, bool includeRelated = false)
        {
            var cacheKey = $"{new T().TableName}_{id}";
            
            // First check if we have a cached instance
            var cachedInstance = GetFromCache(cacheKey);
            if (cachedInstance != null)
            {
                // Check if hash has changed
                if (connection.State != System.Data.ConnectionState.Open)
                    await connection.OpenAsync();

                string hashQuery = $"SELECT __hash_v FROM {new T().TableName} WHERE Id = @Id";
                using var hashCmd = new MySqlCommand(hashQuery, connection);
                hashCmd.Parameters.AddWithValue("@Id", id);
                
                var currentHash = await hashCmd.ExecuteScalarAsync() as string;
                if (IsCacheValid(cacheKey, currentHash))
                {
                    if (includeRelated)
                    {
                        await LoadRelatedDataAsync(connection, cachedInstance);
                    }
                    return cachedInstance;
                }
            }

            // If not cached or hash changed, load from database
            string query = $"SELECT * FROM {new T().TableName} WHERE Id = @Id";
            using var cmd = new MySqlCommand(query, connection);
            cmd.Parameters.AddWithValue("@Id", id);

            T instance = null;
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                {
                    instance = new T();
                    PopulateInstance(reader, instance);
                    instance.UpdateHash();
                    UpdateCache(cacheKey, instance);
                }
            }

            if (instance != null && includeRelated)
            {
                await LoadRelatedDataAsync(connection, instance);
            }

            return instance;
        }

        public static async Task<List<T>> FindAllAsync(MySqlConnection connection, bool includeRelated = false)
        {
            if (connection.State != System.Data.ConnectionState.Open)
                await connection.OpenAsync();

            // First get all IDs and hashes
            var results = new List<T>();
            var hashQuery = $"SELECT Id, __hash_v FROM {new T().TableName}";
            using (var hashCmd = new MySqlCommand(hashQuery, connection))
            {
                using var hashReader = await hashCmd.ExecuteReaderAsync();
                while (await hashReader.ReadAsync())
                {
                    var id = hashReader.GetInt32(0);
                    var hash = hashReader.GetString(1);
                    var cacheKey = $"{new T().TableName}_{id}";

                    var cachedInstance = GetFromCache(cacheKey);
                    if (cachedInstance != null && IsCacheValid(cacheKey, hash))
                    {
                        results.Add(cachedInstance);
                    }
                    else
                    {
                        results.Add(null); // Placeholder for instances that need loading
                    }
                }
            }

            // Load instances that weren't in cache or had different hash
            var missingIndexes = results.Select((item, index) => new { Item = item, Index = index })
                                      .Where(x => x.Item == null)
                                      .Select(x => x.Index)
                                      .ToList();

            if (missingIndexes.Any())
            {
                string query = $"SELECT * FROM {new T().TableName}";
                using var cmd = new MySqlCommand(query, connection);
                using var reader = await cmd.ExecuteReaderAsync();
                
                int currentIndex = 0;
                while (await reader.ReadAsync())
                {
                    if (missingIndexes.Contains(currentIndex))
                    {
                        var instance = new T();
                        PopulateInstance(reader, instance);
                        instance.UpdateHash();
                        UpdateCache($"{new T().TableName}_{instance.Id}", instance);
                        results[currentIndex] = instance;
                    }
                    currentIndex++;
                }
            }

            if (includeRelated)
            {
                foreach (var instance in results)
                {
                    await LoadRelatedDataAsync(connection, instance);
                }
            }

            return results;
        }

        private async Task<T> InsertAsync(MySqlConnection connection)
        {
            if (connection.State != System.Data.ConnectionState.Open)
                await connection.OpenAsync();

            using var transaction = await connection.BeginTransactionAsync();

            try
            {
                UpdateHash(); // Generate initial hash
                string query = GenerateInsertSql();

                using var cmd = new MySqlCommand(query, connection);
                cmd.Transaction = transaction;
                PopulateCommandParameters(cmd);

                object result = await cmd.ExecuteScalarAsync();
                if (result != null)
                {
                    Id = Convert.ToInt32(result);
                    await transaction.CommitAsync();
                    UpdateCache(GetCacheKey(), this as T);
                    return this as T;
                }

                await transaction.RollbackAsync();
                return null;
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        private async Task<T> UpdateAsync(MySqlConnection connection)
        {
            if (connection.State != System.Data.ConnectionState.Open)
                await connection.OpenAsync();

            using var transaction = await connection.BeginTransactionAsync();

            try
            {
                UpdateHash(); // Update hash before saving
                var (setClause, _) = GetUpdateClauseAndPrimaryKeyValue("Id");
                string query = $"UPDATE {TableName} SET {setClause} WHERE Id = @Id";

                using var cmd = new MySqlCommand(query, connection);
                cmd.Transaction = transaction;
                PopulateCommandParameters(cmd);
                cmd.Parameters.AddWithValue("@Id", Id);

                await cmd.ExecuteNonQueryAsync();
                await transaction.CommitAsync();
                UpdateCache(GetCacheKey(), this as T);
                return this as T;
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task DeleteAsync(MySqlConnection connection)
        {
            if (connection.State != System.Data.ConnectionState.Open)
                await connection.OpenAsync();

            using var transaction = await connection.BeginTransactionAsync();

            try
            {
                string query = $"DELETE FROM {TableName} WHERE Id = @Id";
                using var cmd = new MySqlCommand(query, connection);
                cmd.Transaction = transaction;
                cmd.Parameters.AddWithValue("@Id", Id);
                await cmd.ExecuteNonQueryAsync();
                await transaction.CommitAsync();
                
                // Remove from cache
                _cache.Remove(GetCacheKey());
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
    }
}
