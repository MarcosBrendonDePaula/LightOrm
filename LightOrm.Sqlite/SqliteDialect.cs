using System;
using System.Data.Common;
using System.Globalization;
using LightOrm.Core.Attributes;
using LightOrm.Core.Sql;
using Microsoft.Data.Sqlite;

namespace LightOrm.Sqlite
{
    public class SqliteDialect : IDialect
    {
        public string ParameterPrefix => "@";
        public string AutoIncrementClause => "AUTOINCREMENT";
        public bool SupportsIfNotExists => true;
        public bool SupportsCreateIndexIfNotExists => true;

        public string QuoteIdentifier(string name) => $"\"{name.Replace("\"", "\"\"")}\"";

        public string GetLastInsertIdSql() => "SELECT last_insert_rowid();";

        public string GetInsertReturningClause(string idColumnQuoted) => null;

        public string MapType(Type clrType, ColumnAttribute column)
        {
            // SQLite usa type affinity: INTEGER, TEXT, REAL, BLOB, NUMERIC.
            // IsUnsigned é ignorado silenciosamente — SQLite não distingue.
            // Length em VARCHAR também é ignorado pela engine.
            var underlying = Nullable.GetUnderlyingType(clrType) ?? clrType;
            string sqlType;

            if (underlying == typeof(int) || underlying == typeof(long) ||
                underlying == typeof(short) || underlying == typeof(byte) ||
                underlying == typeof(bool))
                sqlType = "INTEGER";
            else if (underlying == typeof(string) || underlying == typeof(Guid) ||
                     underlying == typeof(DateTime))
                sqlType = "TEXT";
            else if (underlying == typeof(float) || underlying == typeof(double))
                sqlType = "REAL";
            else if (underlying == typeof(decimal))
                sqlType = "NUMERIC";
            else if (underlying == typeof(byte[]))
                sqlType = "BLOB";
            else
                throw new NotSupportedException($"Tipo {clrType.Name} não suportado em SQLite.");

            // SQLite só aceita AUTOINCREMENT em coluna INTEGER PRIMARY KEY.
            // Nullability é declarada com NOT NULL; ausência permite NULL.
            return sqlType + (Nullable.GetUnderlyingType(clrType) != null ? "" : " NOT NULL");
        }

        public object ToDbValue(object clrValue, Type clrType)
        {
            if (clrValue == null) return null;
            if (clrValue is bool b) return b ? 1L : 0L;
            if (clrValue is DateTime dt) return dt.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
            if (clrValue is Guid g) return g.ToString("D");
            if (clrValue is decimal m) return m.ToString(CultureInfo.InvariantCulture);
            return clrValue;
        }

        public object FromDbValue(object dbValue, Type clrType)
        {
            if (dbValue == null || dbValue == DBNull.Value) return null;
            var underlying = Nullable.GetUnderlyingType(clrType) ?? clrType;

            if (underlying == typeof(bool))
                return Convert.ToInt64(dbValue) != 0;
            if (underlying == typeof(DateTime))
                return DateTime.Parse(dbValue.ToString(), CultureInfo.InvariantCulture);
            if (underlying == typeof(Guid))
                return Guid.Parse(dbValue.ToString());
            if (underlying == typeof(decimal))
                return decimal.Parse(dbValue.ToString(), CultureInfo.InvariantCulture);

            return Convert.ChangeType(dbValue, underlying, CultureInfo.InvariantCulture);
        }

        public DbCommand CreateCommand(DbConnection connection)
        {
            if (!(connection is SqliteConnection sq))
                throw new ArgumentException("SqliteDialect requer SqliteConnection.", nameof(connection));
            return sq.CreateCommand();
        }
    }
}
