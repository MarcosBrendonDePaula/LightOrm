using System;
using System.Data.Common;
using LightOrm.Core.Attributes;
using LightOrm.Core.Sql;
using MySql.Data.MySqlClient;

namespace LightOrm.MySql
{
    public class MySqlDialect : IDialect
    {
        public string ParameterPrefix => "@";
        public string AutoIncrementClause => "AUTO_INCREMENT";
        public bool SupportsIfNotExists => true;

        public string QuoteIdentifier(string name) => $"`{name.Replace("`", "``")}`";

        public string GetLastInsertIdSql() => "SELECT LAST_INSERT_ID();";

        public string MapType(Type clrType, ColumnAttribute column)
        {
            var underlying = Nullable.GetUnderlyingType(clrType) ?? clrType;
            string sqlType;

            if (underlying == typeof(int))           sqlType = column.IsUnsigned ? "INT UNSIGNED" : "INT";
            else if (underlying == typeof(long))     sqlType = column.IsUnsigned ? "BIGINT UNSIGNED" : "BIGINT";
            else if (underlying == typeof(short))    sqlType = column.IsUnsigned ? "SMALLINT UNSIGNED" : "SMALLINT";
            else if (underlying == typeof(byte))     sqlType = "TINYINT UNSIGNED";
            else if (underlying == typeof(string))   sqlType = $"VARCHAR({(column.Length > 0 ? column.Length : 255)})";
            else if (underlying == typeof(bool))     sqlType = "TINYINT(1)";
            else if (underlying == typeof(DateTime)) sqlType = "DATETIME";
            else if (underlying == typeof(decimal))  sqlType = "DECIMAL(18,2)";
            else if (underlying == typeof(float))    sqlType = "FLOAT";
            else if (underlying == typeof(double))   sqlType = "DOUBLE";
            else if (underlying == typeof(Guid))     sqlType = "CHAR(36)";
            else if (underlying == typeof(byte[]))   sqlType = "BLOB";
            else throw new NotSupportedException($"Tipo {clrType.Name} não suportado em MySQL.");

            return sqlType + (Nullable.GetUnderlyingType(clrType) != null ? " NULL" : " NOT NULL");
        }

        public object ToDbValue(object clrValue, Type clrType)
        {
            if (clrValue == null) return null;
            if (clrValue is Guid g) return g.ToString("D");
            return clrValue;
        }

        public object FromDbValue(object dbValue, Type clrType)
        {
            if (dbValue == null || dbValue == DBNull.Value) return null;
            var underlying = Nullable.GetUnderlyingType(clrType) ?? clrType;
            if (underlying == typeof(Guid))
                return Guid.Parse(dbValue.ToString());
            return Convert.ChangeType(dbValue, underlying);
        }

        public DbCommand CreateCommand(DbConnection connection)
        {
            if (!(connection is MySqlConnection my))
                throw new ArgumentException("MySqlDialect requer MySqlConnection.", nameof(connection));
            return my.CreateCommand();
        }
    }
}
