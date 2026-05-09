using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using LightOrm.Core.Sql;

namespace LightOrm.Core.Migrations
{
    /// <summary>
    /// Descobre, ordena e aplica/reverte migrations. Estilo Laravel: cada
    /// migration é uma classe que herda Migration; ordem é determinada pelo
    /// nome da classe (use prefixo timestamp).
    ///
    /// Mantém histórico em __lightorm_migrations.
    /// </summary>
    public class MigrationRunner
    {
        private const string HistoryTable = "__lightorm_migrations";
        private readonly DbConnection _conn;
        private readonly IDialect _dialect;

        public MigrationRunner(DbConnection connection, IDialect dialect)
        {
            _conn = connection ?? throw new ArgumentNullException(nameof(connection));
            _dialect = dialect ?? throw new ArgumentNullException(nameof(dialect));
        }

        public async Task<List<string>> MigrateAsync(Assembly assembly)
        {
            await EnsureOpenAsync();
            await EnsureHistoryTableAsync();

            var applied = await GetAppliedNamesAsync();
            var migrations = DiscoverMigrations(assembly)
                .Where(m => !applied.Contains(m.Name))
                .OrderBy(m => m.Name, StringComparer.Ordinal)
                .ToList();

            var ran = new List<string>();
            foreach (var migration in migrations)
            {
                using var tx = _conn.BeginTransaction();
                try
                {
                    var schema = new SchemaBuilder(_dialect);
                    await migration.UpAsync(schema);
                    foreach (var sql in schema.Statements)
                        await ExecuteAsync(sql, tx);
                    await RecordAsync(migration.Name, tx);
                    tx.Commit();
                    ran.Add(migration.Name);
                }
                catch
                {
                    tx.Rollback();
                    throw;
                }
            }
            return ran;
        }

        public async Task<string> RollbackAsync(Assembly assembly)
        {
            await EnsureOpenAsync();
            await EnsureHistoryTableAsync();

            var last = await GetLastAppliedAsync();
            if (last == null) return null;

            var migration = DiscoverMigrations(assembly).FirstOrDefault(m => m.Name == last);
            if (migration == null)
                throw new InvalidOperationException(
                    $"Migration '{last}' está registrada mas não foi encontrada no assembly.");

            using var tx = _conn.BeginTransaction();
            try
            {
                var schema = new SchemaBuilder(_dialect);
                await migration.DownAsync(schema);
                foreach (var sql in schema.Statements)
                    await ExecuteAsync(sql, tx);
                await DeleteRecordAsync(last, tx);
                tx.Commit();
                return last;
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        public async Task<List<string>> GetAppliedAsync()
        {
            await EnsureOpenAsync();
            await EnsureHistoryTableAsync();
            return (await GetAppliedNamesAsync()).OrderBy(x => x, StringComparer.Ordinal).ToList();
        }

        // ---------- internals ----------

        private async Task EnsureOpenAsync()
        {
            if (_conn.State != System.Data.ConnectionState.Open)
                await _conn.OpenAsync();
        }

        private async Task EnsureHistoryTableAsync()
        {
            var ifNotExists = _dialect.SupportsIfNotExists ? "IF NOT EXISTS " : "";
            var nameType = _dialect.MapType(typeof(string), new Attributes.ColumnAttribute("name", length: 255));
            var dateType = _dialect.MapType(typeof(DateTime), new Attributes.ColumnAttribute("applied_at"));
            var sql =
                $"CREATE TABLE {ifNotExists}{_dialect.QuoteIdentifier(HistoryTable)} (\n" +
                $"  {_dialect.QuoteIdentifier("name")} {nameType} PRIMARY KEY,\n" +
                $"  {_dialect.QuoteIdentifier("applied_at")} {dateType}\n)";
            await ExecuteAsync(sql, null);
        }

        private async Task<HashSet<string>> GetAppliedNamesAsync()
        {
            var sql = $"SELECT {_dialect.QuoteIdentifier("name")} FROM {_dialect.QuoteIdentifier(HistoryTable)}";
            using var cmd = _dialect.CreateCommand(_conn);
            cmd.CommandText = sql;
            using var reader = await cmd.ExecuteReaderAsync();
            var set = new HashSet<string>(StringComparer.Ordinal);
            while (await reader.ReadAsync())
                if (!reader.IsDBNull(0)) set.Add(reader.GetString(0));
            return set;
        }

        private async Task<string> GetLastAppliedAsync()
        {
            var sql = $"SELECT {_dialect.QuoteIdentifier("name")} FROM {_dialect.QuoteIdentifier(HistoryTable)} " +
                      $"ORDER BY {_dialect.QuoteIdentifier("name")} DESC LIMIT 1";
            using var cmd = _dialect.CreateCommand(_conn);
            cmd.CommandText = sql;
            var v = await cmd.ExecuteScalarAsync();
            return v == null || v == DBNull.Value ? null : (string)v;
        }

        private async Task RecordAsync(string name, DbTransaction tx)
        {
            var sql = $"INSERT INTO {_dialect.QuoteIdentifier(HistoryTable)} " +
                      $"({_dialect.QuoteIdentifier("name")}, {_dialect.QuoteIdentifier("applied_at")}) " +
                      $"VALUES ({_dialect.ParameterPrefix}n, {_dialect.ParameterPrefix}d)";
            using var cmd = _dialect.CreateCommand(_conn);
            cmd.CommandText = sql;
            cmd.Transaction = tx;
            var pn = cmd.CreateParameter(); pn.ParameterName = _dialect.ParameterPrefix + "n"; pn.Value = name;
            var pd = cmd.CreateParameter(); pd.ParameterName = _dialect.ParameterPrefix + "d";
            pd.Value = _dialect.ToDbValue(DateTime.UtcNow, typeof(DateTime)) ?? DBNull.Value;
            cmd.Parameters.Add(pn); cmd.Parameters.Add(pd);
            await cmd.ExecuteNonQueryAsync();
        }

        private async Task DeleteRecordAsync(string name, DbTransaction tx)
        {
            var sql = $"DELETE FROM {_dialect.QuoteIdentifier(HistoryTable)} " +
                      $"WHERE {_dialect.QuoteIdentifier("name")} = {_dialect.ParameterPrefix}n";
            using var cmd = _dialect.CreateCommand(_conn);
            cmd.CommandText = sql;
            cmd.Transaction = tx;
            var p = cmd.CreateParameter(); p.ParameterName = _dialect.ParameterPrefix + "n"; p.Value = name;
            cmd.Parameters.Add(p);
            await cmd.ExecuteNonQueryAsync();
        }

        private async Task ExecuteAsync(string sql, DbTransaction tx)
        {
            using var cmd = _dialect.CreateCommand(_conn);
            cmd.CommandText = sql;
            if (tx != null) cmd.Transaction = tx;
            await cmd.ExecuteNonQueryAsync();
        }

        private static IEnumerable<Migration> DiscoverMigrations(Assembly assembly)
        {
            if (assembly == null) throw new ArgumentNullException(nameof(assembly));
            var migrationType = typeof(Migration);
            foreach (var t in assembly.GetTypes())
            {
                if (t.IsAbstract || !migrationType.IsAssignableFrom(t)) continue;
                if (t.GetConstructor(Type.EmptyTypes) == null)
                    throw new InvalidOperationException(
                        $"Migration {t.Name} precisa de ctor sem parâmetros.");
                yield return (Migration)Activator.CreateInstance(t);
            }
        }
    }
}
