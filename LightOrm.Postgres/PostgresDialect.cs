using System;
using System.Data.Common;
using System.Globalization;
using LightOrm.Core.Attributes;
using LightOrm.Core.Sql;
using Npgsql;

namespace LightOrm.Postgres
{
    public class PostgresDialect : IDialect
    {
        public string ParameterPrefix => "@";
        // Em Postgres, AUTO_INCREMENT é expresso pelo tipo (SERIAL/BIGSERIAL),
        // não por cláusula adicional. MapType cuida disso quando AutoIncrement=true.
        public string AutoIncrementClause => "";
        public bool SupportsIfNotExists => true;
        public bool SupportsCreateIndexIfNotExists => true;

        public string QuoteIdentifier(string name) => $"\"{name.Replace("\"", "\"\"")}\"";

        // Sem função global de last-insert-id em Postgres; usamos RETURNING.
        public string GetLastInsertIdSql() => null;

        public string GetInsertReturningClause(string idColumnQuoted) =>
            $"RETURNING {idColumnQuoted}";

        public string MapType(Type clrType, ColumnAttribute column)
        {
            var underlying = Nullable.GetUnderlyingType(clrType) ?? clrType;
            string sqlType;

            if (column.AutoIncrement && column.IsPrimaryKey)
            {
                if (underlying == typeof(int) || underlying == typeof(short)) sqlType = "SERIAL";
                else if (underlying == typeof(long)) sqlType = "BIGSERIAL";
                else throw new NotSupportedException(
                    $"AutoIncrement em Postgres requer chave inteira; recebido {underlying.Name}.");
                // SERIAL já implica NOT NULL.
                return sqlType;
            }

            if (underlying == typeof(int))           sqlType = "INTEGER";
            else if (underlying == typeof(long))     sqlType = "BIGINT";
            else if (underlying == typeof(short))    sqlType = "SMALLINT";
            else if (underlying == typeof(byte))     sqlType = "SMALLINT";
            else if (underlying == typeof(string))   sqlType = column.Length > 0 ? $"VARCHAR({column.Length})" : "TEXT";
            else if (underlying == typeof(bool))     sqlType = "BOOLEAN";
            else if (underlying == typeof(DateTime)) sqlType = "TIMESTAMP";
            else if (underlying == typeof(decimal))  sqlType = "NUMERIC(18,2)";
            else if (underlying == typeof(float))    sqlType = "REAL";
            else if (underlying == typeof(double))   sqlType = "DOUBLE PRECISION";
            else if (underlying == typeof(Guid))     sqlType = "UUID";
            else if (underlying == typeof(byte[]))   sqlType = "BYTEA";
            else throw new NotSupportedException($"Tipo {clrType.Name} não suportado em Postgres.");

            return sqlType + (Nullable.GetUnderlyingType(clrType) != null ? " NULL" : " NOT NULL");
        }

        public object ToDbValue(object clrValue, Type clrType)
        {
            if (clrValue == null) return null;
            // Npgsql aceita Guid e DateTime nativos; nada a converter.
            return clrValue;
        }

        public object FromDbValue(object dbValue, Type clrType)
        {
            if (dbValue == null || dbValue == DBNull.Value) return null;
            var underlying = Nullable.GetUnderlyingType(clrType) ?? clrType;

            if (underlying == typeof(Guid))
                return dbValue is Guid g ? g : Guid.Parse(dbValue.ToString());
            if (underlying == typeof(byte))
                return Convert.ToByte(dbValue);
            if (underlying == typeof(DateTime))
                return dbValue is DateTime dt ? dt : DateTime.Parse(dbValue.ToString(), CultureInfo.InvariantCulture);
            if (underlying.IsEnum)
                return Enum.ToObject(underlying, dbValue);
            return Convert.ChangeType(dbValue, underlying, CultureInfo.InvariantCulture);
        }

        public DbCommand CreateCommand(DbConnection connection)
        {
            if (!(connection is NpgsqlConnection pg))
                throw new ArgumentException("PostgresDialect requer NpgsqlConnection.", nameof(connection));
            return pg.CreateCommand();
        }
    }
}
