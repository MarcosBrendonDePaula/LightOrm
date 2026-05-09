using System;
using System.Collections.Generic;
using LightOrm.Core.Sql;

namespace LightOrm.Core.Migrations
{
    /// <summary>
    /// Fluent builder estilo Laravel Schema. Coleta operações DDL durante
    /// Up/Down da migration; o MigrationRunner consome a lista e executa.
    /// </summary>
    public class SchemaBuilder
    {
        private readonly IDialect _dialect;
        private readonly List<string> _statements = new List<string>();

        internal SchemaBuilder(IDialect dialect)
        {
            _dialect = dialect ?? throw new ArgumentNullException(nameof(dialect));
        }

        internal IReadOnlyList<string> Statements => _statements;

        /// <summary>Cria uma tabela. O builder coleta colunas, FKs, índices.</summary>
        public SchemaBuilder Create(string tableName, Action<TableBuilder> build)
        {
            if (string.IsNullOrEmpty(tableName)) throw new ArgumentException("Table name vazio.", nameof(tableName));
            if (build == null) throw new ArgumentNullException(nameof(build));

            var t = new TableBuilder(_dialect, tableName);
            build(t);
            foreach (var sql in t.GetCreateStatements()) _statements.Add(sql);
            return this;
        }

        /// <summary>Altera tabela existente. Add/drop coluna, add índice.</summary>
        public SchemaBuilder Alter(string tableName, Action<AlterTableBuilder> build)
        {
            var a = new AlterTableBuilder(_dialect, tableName);
            build(a);
            foreach (var sql in a.GetStatements()) _statements.Add(sql);
            return this;
        }

        /// <summary>DROP TABLE.</summary>
        public SchemaBuilder Drop(string tableName)
        {
            _statements.Add($"DROP TABLE {_dialect.QuoteIdentifier(tableName)}");
            return this;
        }

        /// <summary>DROP TABLE IF EXISTS.</summary>
        public SchemaBuilder DropIfExists(string tableName)
        {
            var ifExists = _dialect.SupportsIfNotExists ? "IF EXISTS " : "";
            _statements.Add($"DROP TABLE {ifExists}{_dialect.QuoteIdentifier(tableName)}");
            return this;
        }

        /// <summary>RENAME TABLE. Sintaxe varia entre dialects mas todos suportam.</summary>
        public SchemaBuilder Rename(string from, string to)
        {
            _statements.Add($"ALTER TABLE {_dialect.QuoteIdentifier(from)} RENAME TO {_dialect.QuoteIdentifier(to)}");
            return this;
        }

        /// <summary>Executa SQL bruto — escape hatch.</summary>
        public SchemaBuilder Raw(string sql)
        {
            if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentException("SQL vazio.", nameof(sql));
            _statements.Add(sql);
            return this;
        }
    }
}
