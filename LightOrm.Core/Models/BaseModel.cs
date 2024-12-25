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

        [Column("CreatedAt")]
        public DateTime CreatedAt { get; set; }

        [Column("UpdatedAt")]
        public DateTime UpdatedAt { get; set; }

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
                                object convertedValue = Convert.ChangeType(value,
                                    Nullable.GetUnderlyingType(relatedProperty.PropertyType) ?? relatedProperty.PropertyType);
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
            foreach (var property in typeof(T).GetProperties())
            {
                // Handle ManyToMany relationships
                var manyToManyAttr = property.GetCustomAttribute<ManyToManyAttribute>();
                if (manyToManyAttr != null)
                {
                    if (connection.State != System.Data.ConnectionState.Open)
                        await connection.OpenAsync();

                    var relatedType = manyToManyAttr.RelatedType;
                    string query = $@"
                        SELECT r.* 
                        FROM {manyToManyAttr.AssociationTable} a
                        JOIN {GetTableNameFromType(relatedType)} r ON r.Id = a.{manyToManyAttr.TargetForeignKey}
                        WHERE a.{manyToManyAttr.SourceForeignKey} = @Id";

                    using var cmd = new MySqlCommand(query, connection);
                    cmd.Parameters.AddWithValue("@Id", instance.Id);

                    var relatedItems = new List<object>();
                    using (var reader = await cmd.ExecuteReaderAsync() as MySqlDataReader)
                    {
                        while (await reader.ReadAsync())
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
                                            object convertedValue = Convert.ChangeType(value,
                                                Nullable.GetUnderlyingType(relatedProperty.PropertyType) ?? relatedProperty.PropertyType);
                                            relatedProperty.SetValue(relatedInstance, convertedValue);
                                        }
                                    }
                                }
                            }
                            relatedItems.Add(relatedInstance);
                        }
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

                // Handle OneToMany relationships
                var oneToManyAttr = property.GetCustomAttribute<OneToManyAttribute>();
                if (oneToManyAttr != null)
                {
                    if (connection.State != System.Data.ConnectionState.Open)
                        await connection.OpenAsync();

                    var relatedType = oneToManyAttr.RelatedType;
                    string query = $"SELECT * FROM {GetTableNameFromType(relatedType)} WHERE {oneToManyAttr.ForeignKeyProperty} = @Id";
                    
                    using var cmd = new MySqlCommand(query, connection);
                    cmd.Parameters.AddWithValue("@Id", instance.Id);

                    var relatedItems = new List<object>();
                    using (var reader = await cmd.ExecuteReaderAsync() as MySqlDataReader)
                    {
                        while (await reader.ReadAsync())
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
                                            object convertedValue = Convert.ChangeType(value,
                                                Nullable.GetUnderlyingType(relatedProperty.PropertyType) ?? relatedProperty.PropertyType);
                                            relatedProperty.SetValue(relatedInstance, convertedValue);
                                        }
                                    }
                                }
                            }
                            relatedItems.Add(relatedInstance);
                        }
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

                // Handle OneToOne and ForeignKey relationships
                var oneToOneAttr = property.GetCustomAttribute<OneToOneAttribute>();
                var foreignKeyAttr = property.GetCustomAttribute<ForeignKeyAttribute>();

                if (oneToOneAttr != null || foreignKeyAttr != null)
                {
                    if (oneToOneAttr != null)
                    {
                        // For OneToOne navigation property, get the foreign key property
                        var foreignKeyProperty = typeof(T).GetProperty(oneToOneAttr.ForeignKeyProperty);
                        if (foreignKeyProperty == null)
                        {
                            throw new InvalidOperationException($"Foreign key property {oneToOneAttr.ForeignKeyProperty} not found for {property.Name}");
                        }
                        var foreignKeyValue = foreignKeyProperty.GetValue(instance);
                        if (foreignKeyValue != null)
                        {
                            await LoadRelatedEntityAsync(connection, instance, property, oneToOneAttr.RelatedType, foreignKeyValue);
                        }
                    }
                    else if (foreignKeyAttr != null)
                    {
                        // For foreign key property, get the navigation property
                        var navigationPropertyName = property.Name.Replace("Id", "");
                        var navigationProperty = typeof(T).GetProperty(navigationPropertyName);
                        if (navigationProperty != null)
                        {
                            var foreignKeyValue = property.GetValue(instance);
                            if (foreignKeyValue != null)
                            {
                                var relatedType = GetModelTypeByTableName(foreignKeyAttr.ReferenceTable);
                                if (relatedType != null)
                                {
                                    await LoadRelatedEntityAsync(connection, instance, navigationProperty, relatedType, foreignKeyValue);
                                }
                            }
                        }
                    }
                }
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

        protected static void PopulateInstance(MySqlDataReader reader, IModel instance)
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
                            object convertedValue = Convert.ChangeType(
                                value, 
                                Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType);
                            property.SetValue(instance, convertedValue);
                        }
                    }
                }
            }
        }

        private (string columns, string parameters) GetColumnAndParameterLists()
        {
            var columnNames = new List<string>();
            var parameterNames = new List<string>();

            foreach (var property in typeof(T).GetProperties())
            {
                var columnAttr = property.GetCustomAttribute<ColumnAttribute>();
                if (columnAttr != null && !columnAttr.AutoIncrement)
                {
                    columnNames.Add(columnAttr.Name);
                    parameterNames.Add($"@{columnAttr.Name}");
                }
            }

            return (string.Join(", ", columnNames), string.Join(", ", parameterNames));
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
                    else if (!columnAttr.AutoIncrement)
                    {
                        setClauseList.Add($"{columnAttr.Name} = @{columnAttr.Name}");
                    }
                }
            }

            return (string.Join(", ", setClauseList), primaryKeyValue);
        }

        private void PopulateCommandParameters(MySqlCommand cmd)
        {
            foreach (var property in typeof(T).GetProperties())
            {
                var columnAttr = property.GetCustomAttribute<ColumnAttribute>();
                if (columnAttr != null && !columnAttr.AutoIncrement)
                {
                    string parameterName = $"@{columnAttr.Name}";
                    if (!cmd.Parameters.Contains(parameterName))
                    {
                        object value = property.GetValue(this);
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
            var foreignKeys = new List<string>();

            foreach (var property in typeof(T).GetProperties())
            {
                var columnAttr = property.GetCustomAttribute<ColumnAttribute>();
                if (columnAttr != null)
                {
                    string sqlType = GetSqlType(property.PropertyType, columnAttr);
                    string columnDef = $"{columnAttr.Name} {sqlType}";

                    if (columnAttr.IsPrimaryKey)
                        columnDef += " PRIMARY KEY";
                    if (columnAttr.AutoIncrement)
                        columnDef += " AUTO_INCREMENT";

                    columns.Add(columnDef);

                    var foreignKeyAttr = property.GetCustomAttribute<ForeignKeyAttribute>();
                    if (foreignKeyAttr != null)
                    {
                        foreignKeys.Add($"FOREIGN KEY ({columnAttr.Name}) REFERENCES {foreignKeyAttr.ReferenceTable}({foreignKeyAttr.ReferenceColumn})");
                    }
                }
            }

            string sql = $"CREATE TABLE IF NOT EXISTS {TableName} ({string.Join(", ", columns)}";
            if (foreignKeys.Count > 0)
                sql += $", {string.Join(", ", foreignKeys)}";
            sql += ");";

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
                Type t when t == typeof(decimal) => "DECIMAL(18,2)",
                Type t when t == typeof(float) => "FLOAT",
                Type t when t == typeof(double) => "DOUBLE",
                _ => throw new NotSupportedException($"Type {type.Name} is not supported for SQL mapping.")
            };

            // If the type is nullable, make the column nullable
            if (Nullable.GetUnderlyingType(type) != null)
            {
                sqlType += " NULL";
            }
            else
            {
                sqlType += " NOT NULL";
            }

            return sqlType;
        }

        public async Task<T> SaveAsync(MySqlConnection connection)
        {
            if (Id == 0)
            {
                CreatedAt = DateTime.UtcNow;
                UpdatedAt = CreatedAt;
                return await InsertAsync(connection);
            }
            else
            {
                UpdatedAt = DateTime.UtcNow;
                return await UpdateAsync(connection);
            }
        }

        private async Task<T> InsertAsync(MySqlConnection connection)
        {
            if (connection.State != System.Data.ConnectionState.Open)
                await connection.OpenAsync();

            using var transaction = await connection.BeginTransactionAsync();

            try
            {
                var (columns, parameters) = GetColumnAndParameterLists();
                string query = $@"
                    INSERT INTO {TableName} ({columns}) 
                    VALUES ({parameters});
                    SELECT LAST_INSERT_ID();";

                using var cmd = new MySqlCommand(query, connection);
                cmd.Transaction = transaction;
                PopulateCommandParameters(cmd);

                object result = await cmd.ExecuteScalarAsync();
                if (result != null)
                {
                    Id = Convert.ToInt32(result);
                    await transaction.CommitAsync();
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
                var (setClause, _) = GetUpdateClauseAndPrimaryKeyValue("Id");
                string query = $"UPDATE {TableName} SET {setClause} WHERE Id = @Id";

                using var cmd = new MySqlCommand(query, connection);
                cmd.Transaction = transaction;
                PopulateCommandParameters(cmd);
                cmd.Parameters.AddWithValue("@Id", Id);

                await cmd.ExecuteNonQueryAsync();
                await transaction.CommitAsync();
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
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public static async Task<T> FindByIdAsync(MySqlConnection connection, int id, bool includeRelated = false)
        {
            if (connection.State != System.Data.ConnectionState.Open)
                await connection.OpenAsync();

            string query = $"SELECT * FROM {new T().TableName} WHERE Id = @Id";
            using var cmd = new MySqlCommand(query, connection);
            cmd.Parameters.AddWithValue("@Id", id);

            T instance = null;
            using (var reader = await cmd.ExecuteReaderAsync() as MySqlDataReader)
            {
                if (await reader.ReadAsync())
                {
                    instance = new T();
                    PopulateInstance(reader, instance);
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
            var results = new List<T>();
            
            if (connection.State != System.Data.ConnectionState.Open)
                await connection.OpenAsync();

            string query = $"SELECT * FROM {new T().TableName}";
            using var cmd = new MySqlCommand(query, connection);

            using (var reader = await cmd.ExecuteReaderAsync() as MySqlDataReader)
            {
                while (await reader.ReadAsync())
                {
                    var instance = new T();
                    PopulateInstance(reader, instance);
                    results.Add(instance);
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
    }
}
