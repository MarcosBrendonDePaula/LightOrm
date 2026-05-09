using System;
using System.Collections.Generic;
using System.Linq;
using LightOrm.Core.Attributes;
using LightOrm.Core.Sql;

namespace LightOrm.Core.Migrations
{
    /// <summary>
    /// Builder de colunas dentro de Schema.Create(...). Cada método devolve
    /// ColumnBuilder pra modificadores (Nullable/Unique/Default/Index/etc.).
    /// </summary>
    public class TableBuilder
    {
        private readonly IDialect _dialect;
        private readonly string _tableName;
        private readonly List<ColumnBuilder> _columns = new List<ColumnBuilder>();
        private readonly List<(string col, string refTable, string refCol)> _foreignKeys
            = new List<(string, string, string)>();
        private readonly List<(string name, string col, bool unique)> _indexes
            = new List<(string, string, bool)>();

        internal TableBuilder(IDialect dialect, string tableName)
        {
            _dialect = dialect;
            _tableName = tableName;
        }

        internal string TableName => _tableName;
        internal IDialect Dialect => _dialect;

        /// <summary>Coluna PK auto-increment INT (atalho Laravel).</summary>
        public ColumnBuilder Id(string name = "Id")
        {
            var col = new ColumnBuilder(this, name, typeof(int),
                new ColumnAttribute(name, isPrimaryKey: true, autoIncrement: true));
            _columns.Add(col);
            return col;
        }

        public ColumnBuilder Int(string name) => Add(name, typeof(int));
        public ColumnBuilder Long(string name) => Add(name, typeof(long));
        public ColumnBuilder Short(string name) => Add(name, typeof(short));
        public ColumnBuilder Bool(string name) => Add(name, typeof(bool));
        public ColumnBuilder DateTime(string name) => Add(name, typeof(DateTime));
        public ColumnBuilder Decimal(string name) => Add(name, typeof(decimal));
        public ColumnBuilder Float(string name) => Add(name, typeof(float));
        public ColumnBuilder Double(string name) => Add(name, typeof(double));
        public ColumnBuilder Guid(string name) => Add(name, typeof(System.Guid));
        public ColumnBuilder Bytes(string name) => Add(name, typeof(byte[]));

        public ColumnBuilder String(string name, int length = 255)
        {
            var col = new ColumnBuilder(this, name, typeof(string),
                new ColumnAttribute(name, length: length));
            _columns.Add(col);
            return col;
        }

        /// <summary>Adiciona CreatedAt e UpdatedAt (atalho Laravel).</summary>
        public TableBuilder Timestamps()
        {
            DateTime("CreatedAt");
            DateTime("UpdatedAt");
            return this;
        }

        /// <summary>Adiciona DeletedAt nullable (soft delete).</summary>
        public TableBuilder SoftDeletes(string columnName = "deleted_at")
        {
            DateTime(columnName).Nullable();
            return this;
        }

        private ColumnBuilder Add(string name, Type clrType)
        {
            var col = new ColumnBuilder(this, name, clrType, new ColumnAttribute(name));
            _columns.Add(col);
            return col;
        }

        internal void RegisterForeignKey(string col, string refTable, string refCol)
            => _foreignKeys.Add((col, refTable, refCol));
        internal void RegisterIndex(string name, string col, bool unique)
            => _indexes.Add((name, col, unique));

        internal IEnumerable<string> GetCreateStatements()
        {
            var defs = new List<string>();
            foreach (var c in _columns) defs.Add(c.BuildColumnDef());
            foreach (var (col, refTable, refCol) in _foreignKeys)
                defs.Add($"FOREIGN KEY ({_dialect.QuoteIdentifier(col)}) " +
                         $"REFERENCES {_dialect.QuoteIdentifier(refTable)}({_dialect.QuoteIdentifier(refCol)})");

            var ifNotExists = _dialect.SupportsIfNotExists ? "IF NOT EXISTS " : "";
            yield return $"CREATE TABLE {ifNotExists}{_dialect.QuoteIdentifier(_tableName)} (\n  " +
                         string.Join(",\n  ", defs) + "\n)";

            var idxIfNotExists = _dialect.SupportsCreateIndexIfNotExists ? "IF NOT EXISTS " : "";
            foreach (var (name, col, unique) in _indexes)
            {
                var keyword = unique ? "UNIQUE INDEX" : "INDEX";
                yield return $"CREATE {keyword} {idxIfNotExists}{_dialect.QuoteIdentifier(name)} " +
                             $"ON {_dialect.QuoteIdentifier(_tableName)}({_dialect.QuoteIdentifier(col)})";
            }
        }
    }
}
