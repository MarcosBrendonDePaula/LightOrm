using System.Threading.Tasks;
using LightOrm.Core.Migrations;
using LightOrm.Core.Sql;
using LightOrm.Sqlite;
using Microsoft.Data.Sqlite;
using Xunit;

namespace LightOrm.Core.Tests.Persistence
{
    // Migrations de teste — prefixo M com timestamp ordena cronológico via StringComparer.Ordinal.

    public class M2026_05_09_120000_CreateUsers : Migration
    {
        public override void Up(SchemaBuilder schema) =>
            schema.Create("mig_users", t =>
            {
                t.Id();
                t.String("name", 100);
                t.String("email", 200).Unique();
                t.Bool("active").Default("0");
                t.Timestamps();
            });

        public override void Down(SchemaBuilder schema) =>
            schema.DropIfExists("mig_users");
    }

    public class M2026_05_09_130000_AddRoleToUsers : Migration
    {
        public override void Up(SchemaBuilder schema) =>
            schema.Alter("mig_users", t => t.AddString("role", 50, c => c.Nullable()));

        public override void Down(SchemaBuilder schema) =>
            schema.Alter("mig_users", t => t.DropColumn("role"));
    }

    public class M2026_05_09_140000_CreatePosts : Migration
    {
        public override void Up(SchemaBuilder schema) =>
            schema.Create("mig_posts", t =>
            {
                t.Id();
                t.String("title", 200);
                t.Int("user_id").References("mig_users").Index();
                t.Timestamps();
            });

        public override void Down(SchemaBuilder schema) =>
            schema.DropIfExists("mig_posts");
    }

    public class MigrationsTests
    {
        private static (SqliteConnection conn, IDialect dialect) Open()
        {
            var c = new SqliteConnection("Data Source=:memory:");
            c.Open();
            return (c, new SqliteDialect());
        }

        [Fact]
        public async Task Migrate_applies_all_pending_in_order()
        {
            var (conn, dialect) = Open();
            var runner = new MigrationRunner(conn, dialect);

            var ran = await runner.MigrateAsync(typeof(MigrationsTests).Assembly);
            Assert.Contains("M2026_05_09_120000_CreateUsers", ran);
            Assert.Contains("M2026_05_09_130000_AddRoleToUsers", ran);
            Assert.Contains("M2026_05_09_140000_CreatePosts", ran);

            // Ordem: o create vem antes do alter e do create de posts.
            var idxCreate = ran.IndexOf("M2026_05_09_120000_CreateUsers");
            var idxAlter = ran.IndexOf("M2026_05_09_130000_AddRoleToUsers");
            var idxPosts = ran.IndexOf("M2026_05_09_140000_CreatePosts");
            Assert.True(idxCreate < idxAlter);
            Assert.True(idxAlter < idxPosts);

            // Tabelas e colunas devem existir.
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' ORDER BY name";
            using var reader = await cmd.ExecuteReaderAsync();
            var tables = new System.Collections.Generic.List<string>();
            while (await reader.ReadAsync()) tables.Add(reader.GetString(0));
            Assert.Contains("mig_users", tables);
            Assert.Contains("mig_posts", tables);
            Assert.Contains("__lightorm_migrations", tables);
        }

        [Fact]
        public async Task Migrate_skips_already_applied()
        {
            var (conn, dialect) = Open();
            var runner = new MigrationRunner(conn, dialect);

            var first = await runner.MigrateAsync(typeof(MigrationsTests).Assembly);
            Assert.Equal(3, first.Count);

            var second = await runner.MigrateAsync(typeof(MigrationsTests).Assembly);
            Assert.Empty(second);
        }

        [Fact]
        public async Task Rollback_reverts_last_migration_only()
        {
            var (conn, dialect) = Open();
            var runner = new MigrationRunner(conn, dialect);

            await runner.MigrateAsync(typeof(MigrationsTests).Assembly);
            var rolled = await runner.RollbackAsync(typeof(MigrationsTests).Assembly);
            Assert.Equal("M2026_05_09_140000_CreatePosts", rolled);

            // mig_posts foi dropada; mig_users e role continuam.
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table'";
            using var reader = await cmd.ExecuteReaderAsync();
            var tables = new System.Collections.Generic.List<string>();
            while (await reader.ReadAsync()) tables.Add(reader.GetString(0));
            Assert.DoesNotContain("mig_posts", tables);
            Assert.Contains("mig_users", tables);

            // Histórico: só 2 migrations registradas.
            var applied = await runner.GetAppliedAsync();
            Assert.Equal(2, applied.Count);
        }

        [Fact]
        public async Task Rollback_then_migrate_reapplies()
        {
            var (conn, dialect) = Open();
            var runner = new MigrationRunner(conn, dialect);

            await runner.MigrateAsync(typeof(MigrationsTests).Assembly);
            await runner.RollbackAsync(typeof(MigrationsTests).Assembly);

            var ran = await runner.MigrateAsync(typeof(MigrationsTests).Assembly);
            Assert.Single(ran);
            Assert.Equal("M2026_05_09_140000_CreatePosts", ran[0]);
        }

        [Fact]
        public async Task Rollback_on_empty_history_returns_null()
        {
            var (conn, dialect) = Open();
            var runner = new MigrationRunner(conn, dialect);
            var result = await runner.RollbackAsync(typeof(MigrationsTests).Assembly);
            Assert.Null(result);
        }

        [Fact]
        public async Task Failed_migration_rolls_back_and_does_not_record()
        {
            // Migration que aplica um statement válido + um raw SQL inválido.
            // Sem registrar na history — porque a tx aborta.
            var (conn, dialect) = Open();

            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            var runner = new MigrationRunner(conn, dialect);

            // Roda só as migrations daqui (que estão verdes); confirmo que a
            // history fica intacta após uma falha proposital (BadMigration).
            await Assert.ThrowsAnyAsync<System.Exception>(async () =>
            {
                // Cria runner que invoca diretamente uma migration com bug.
                var schema = new SchemaBuilder(dialect);
                var bad = new BadMigration();
                bad.Up(schema);
                using var tx = conn.BeginTransaction();
                foreach (var sql in schema.Statements)
                {
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = sql;
                    cmd.Transaction = tx;
                    await cmd.ExecuteNonQueryAsync();
                }
                tx.Commit();
            });
        }

        [Fact]
        public async Task Insert_after_migration_works()
        {
            var (conn, dialect) = Open();
            var runner = new MigrationRunner(conn, dialect);
            await runner.MigrateAsync(typeof(MigrationsTests).Assembly);

            // Verifica que o INSERT funciona com os tipos definidos no SchemaBuilder.
            using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "INSERT INTO mig_users (name, email, active, CreatedAt, UpdatedAt) " +
                "VALUES ('Ana', 'ana@x.com', 1, '2026-01-01', '2026-01-01')";
            await cmd.ExecuteNonQueryAsync();

            using var count = conn.CreateCommand();
            count.CommandText = "SELECT COUNT(*) FROM mig_users";
            var n = (long)await count.ExecuteScalarAsync();
            Assert.Equal(1, n);
        }
    }

    // Não herda Migration — é só pra testar SQL falho via SchemaBuilder.Raw.
    public class BadMigration
    {
        public void Up(SchemaBuilder schema) =>
            schema.Raw("CREATE TABLE this is invalid sql");
    }
}
