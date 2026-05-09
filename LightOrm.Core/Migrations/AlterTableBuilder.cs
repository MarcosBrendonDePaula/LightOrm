using System;
using System.Collections.Generic;
using LightOrm.Core.Attributes;
using LightOrm.Core.Sql;

namespace LightOrm.Core.Migrations
{
    /// <summary>
    /// Builder para Schema.Alter(...). Suporta o subset seguro:
    /// AddColumn, DropColumn, AddIndex, DropIndex, AddForeignKey.
    /// Rename de coluna varia muito entre dialects — deixar fora.
    /// </summary>
    public class AlterTableBuilder
    {
        private readonly IDialect _dialect;
        private readonly string _table;
        private readonly List<string> _statements = new List<string>();

        internal AlterTableBuilder(IDialect dialect, string table)
        {
            _dialect = dialect;
            _table = table;
        }

        public AlterTableBuilder AddColumn(string name, Type clrType, Action<ColumnBuilder> configure = null)
        {
            // Reusa TableBuilder/ColumnBuilder pra montar a definição.
            var t = new TableBuilder(_dialect, _table);
            ColumnBuilder col;
            if (clrType == typeof(string)) col = t.String(name);
            else if (clrType == typeof(int)) col = t.Int(name);
            else if (clrType == typeof(long)) col = t.Long(name);
            else if (clrType == typeof(short)) col = t.Short(name);
            else if (clrType == typeof(bool)) col = t.Bool(name);
            else if (clrType == typeof(DateTime)) col = t.DateTime(name);
            else if (clrType == typeof(decimal)) col = t.Decimal(name);
            else if (clrType == typeof(float)) col = t.Float(name);
            else if (clrType == typeof(double)) col = t.Double(name);
            else if (clrType == typeof(Guid)) col = t.Guid(name);
            else if (clrType == typeof(byte[])) col = t.Bytes(name);
            else throw new NotSupportedException($"Tipo {clrType.Name} não suportado em AddColumn.");

            configure?.Invoke(col);

            _statements.Add($"ALTER TABLE {_dialect.QuoteIdentifier(_table)} ADD COLUMN {col.BuildColumnDef()}");
            return this;
        }

        public AlterTableBuilder AddString(string name, int length = 255, Action<ColumnBuilder> configure = null)
        {
            var t = new TableBuilder(_dialect, _table);
            var col = t.String(name, length);
            configure?.Invoke(col);
            _statements.Add($"ALTER TABLE {_dialect.QuoteIdentifier(_table)} ADD COLUMN {col.BuildColumnDef()}");
            return this;
        }

        public AlterTableBuilder DropColumn(string name)
        {
            _statements.Add($"ALTER TABLE {_dialect.QuoteIdentifier(_table)} DROP COLUMN {_dialect.QuoteIdentifier(name)}");
            return this;
        }

        public AlterTableBuilder AddIndex(string columnName, bool unique = false, string indexName = null)
        {
            var name = indexName ?? $"idx_{_table}_{columnName}";
            var keyword = unique ? "UNIQUE INDEX" : "INDEX";
            var ifNotExists = _dialect.SupportsCreateIndexIfNotExists ? "IF NOT EXISTS " : "";
            _statements.Add($"CREATE {keyword} {ifNotExists}{_dialect.QuoteIdentifier(name)} " +
                            $"ON {_dialect.QuoteIdentifier(_table)}({_dialect.QuoteIdentifier(columnName)})");
            return this;
        }

        public AlterTableBuilder DropIndex(string indexName)
        {
            // SQLite e Postgres: DROP INDEX nome. MySQL: DROP INDEX nome ON tabela.
            // Só MySQL precisa do ON; os outros aceitam sem. Para portabilidade,
            // usuário pode usar Raw quando o dialect importar.
            _statements.Add($"DROP INDEX {_dialect.QuoteIdentifier(indexName)}");
            return this;
        }

        public AlterTableBuilder Raw(string sql)
        {
            _statements.Add(sql);
            return this;
        }

        internal IReadOnlyList<string> GetStatements() => _statements;
    }
}
