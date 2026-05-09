using System;
using LightOrm.Core.Attributes;

namespace LightOrm.Core.Migrations
{
    /// <summary>
    /// Modificadores fluent de uma coluna: Nullable, Unique, Default, Index,
    /// Primary, References. Encadeáveis.
    /// </summary>
    public class ColumnBuilder
    {
        private readonly TableBuilder _parent;
        private readonly string _name;
        private Type _clrType;
        private ColumnAttribute _column;
        private bool _nullable;
        private string _defaultLiteral;   // SQL literal (já com aspas se string).
        private bool _isUnique;
        private bool _hasIndex;
        private string _indexName;

        internal ColumnBuilder(TableBuilder parent, string name, Type clrType, ColumnAttribute attr)
        {
            _parent = parent;
            _name = name;
            _clrType = clrType;
            _column = attr;
        }

        public ColumnBuilder Nullable()
        {
            _nullable = true;
            // Reflete no clrType pra MapType emitir NULL: usa Nullable<T> para value types.
            if (_clrType.IsValueType && System.Nullable.GetUnderlyingType(_clrType) == null)
                _clrType = typeof(System.Nullable<>).MakeGenericType(_clrType);
            return this;
        }

        public ColumnBuilder Unsigned()
        {
            _column = new ColumnAttribute(_column.Name, _column.IsPrimaryKey, _column.AutoIncrement,
                _column.Length, isUnsigned: true);
            return this;
        }

        public ColumnBuilder Primary()
        {
            _column = new ColumnAttribute(_column.Name, isPrimaryKey: true, _column.AutoIncrement,
                _column.Length, _column.IsUnsigned);
            return this;
        }

        public ColumnBuilder AutoIncrement()
        {
            _column = new ColumnAttribute(_column.Name, _column.IsPrimaryKey, autoIncrement: true,
                _column.Length, _column.IsUnsigned);
            return this;
        }

        public ColumnBuilder Unique()
        {
            _isUnique = true;
            _parent.RegisterIndex($"uq_{_parent.TableName}_{_name}", _name, unique: true);
            return this;
        }

        public ColumnBuilder Index(string indexName = null)
        {
            _hasIndex = true;
            _indexName = indexName ?? $"idx_{_parent.TableName}_{_name}";
            _parent.RegisterIndex(_indexName, _name, unique: false);
            return this;
        }

        /// <summary>
        /// Define DEFAULT no DDL. Para valores string passe com aspas: Default("'pending'").
        /// Para SQL functions: Default("CURRENT_TIMESTAMP").
        /// </summary>
        public ColumnBuilder Default(string sqlLiteral)
        {
            _defaultLiteral = sqlLiteral;
            return this;
        }

        public ColumnBuilder References(string refTable, string refCol = "Id")
        {
            _parent.RegisterForeignKey(_name, refTable, refCol);
            return this;
        }

        internal string BuildColumnDef()
        {
            var dialect = _parent.Dialect;
            var sqlType = dialect.MapType(_clrType, _column);

            // Nullable() em reference type (string, byte[]) precisa sobrescrever
            // o "NOT NULL" que o dialect emite por default. Para value types,
            // o _clrType já foi trocado por Nullable<T> em Nullable() e o
            // dialect respeitou.
            if (_nullable)
            {
                sqlType = sqlType.Replace(" NOT NULL", "");
                if (!sqlType.Contains(" NULL")) sqlType += " NULL";
            }

            var def = $"{dialect.QuoteIdentifier(_name)} {sqlType}";

            if (_column.IsPrimaryKey) def += " PRIMARY KEY";
            if (_column.AutoIncrement && !string.IsNullOrEmpty(dialect.AutoIncrementClause))
                def += " " + dialect.AutoIncrementClause;
            if (!string.IsNullOrEmpty(_defaultLiteral)) def += " DEFAULT " + _defaultLiteral;

            return def;
        }
    }
}
