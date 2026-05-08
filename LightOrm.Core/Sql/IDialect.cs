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

        string QuoteIdentifier(string name);
        string GetLastInsertIdSql();
        string MapType(Type clrType, ColumnAttribute column);

        object ToDbValue(object clrValue, Type clrType);
        object FromDbValue(object dbValue, Type clrType);

        DbCommand CreateCommand(DbConnection connection);
    }
}
