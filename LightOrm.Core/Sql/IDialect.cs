using System;
using System.Data.Common;
using LightOrm.Core.Attributes;

namespace LightOrm.Core.Sql
{
    public interface IDialect
    {
        string ParameterPrefix { get; }
        string AutoIncrementClause { get; }
        bool SupportsIfNotExists { get; }
        bool SupportsCreateIndexIfNotExists { get; }

        string QuoteIdentifier(string name);
        string GetLastInsertIdSql();

        // Permite ao dialect anexar uma cláusula RETURNING ao INSERT (Postgres).
        // Dialects que usam função separada de last-insert-id devolvem null aqui.
        string GetInsertReturningClause(string idColumnQuoted);

        string MapType(Type clrType, ColumnAttribute column);

        object ToDbValue(object clrValue, Type clrType);
        object FromDbValue(object dbValue, Type clrType);

        DbCommand CreateCommand(DbConnection connection);
    }
}
