using System.Threading.Tasks;
using LightOrm.Core.Sql;
using LightOrm.Core.Tests.Models;
using LightOrm.Sqlite;
using Microsoft.Data.Sqlite;
using Xunit;

namespace LightOrm.Core.Tests
{
    public class DdlAttributesTests
    {
        private static (SqliteConnection conn, IDialect dialect) Open()
        {
            var c = new SqliteConnection("Data Source=:memory:");
            c.Open();
            return (c, new SqliteDialect());
        }

        [Fact]
        public async Task Table_attribute_provides_name_when_not_overridden()
        {
            var (conn, dialect) = Open();
            var repo = new SqlRepository<TableAttributeModel, int>(conn, dialect);
            await repo.EnsureSchemaAsync();

            await repo.SaveAsync(new TableAttributeModel { Name = "x" });

            // Confirma que a tabela foi criada com o nome do [Table].
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='table_attr_demo'";
            var found = (string)await cmd.ExecuteScalarAsync();
            Assert.Equal("table_attr_demo", found);
        }

        [Fact]
        public async Task Unique_attribute_creates_unique_index_and_blocks_duplicates()
        {
            var (conn, dialect) = Open();
            var repo = new SqlRepository<IndexedModel, int>(conn, dialect);
            await repo.EnsureSchemaAsync();

            await repo.SaveAsync(new IndexedModel { Email = "a@x.com", First = "A", Last = "X", CreatedYear = 2020 });

            // Mesmo email, segunda inserção falha por causa do UNIQUE INDEX.
            var ex = await Assert.ThrowsAnyAsync<SqliteException>(() =>
                repo.SaveAsync(new IndexedModel { Email = "a@x.com", First = "B", Last = "Y", CreatedYear = 2021 }));
            Assert.Contains("UNIQUE", ex.Message, System.StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Composite_index_groups_columns_under_same_name()
        {
            var (conn, dialect) = Open();
            var repo = new SqlRepository<IndexedModel, int>(conn, dialect);
            await repo.EnsureSchemaAsync();

            // SQLite expõe o índice via sqlite_master + index_info.
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT sql FROM sqlite_master WHERE type='index' AND name='idx_full_name'";
            var sql = (string)await cmd.ExecuteScalarAsync();
            Assert.NotNull(sql);
            Assert.Contains("first", sql);
            Assert.Contains("last", sql);
        }

        [Fact]
        public async Task Anonymous_index_is_created()
        {
            var (conn, dialect) = Open();
            var repo = new SqlRepository<IndexedModel, int>(conn, dialect);
            await repo.EnsureSchemaAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT count(*) FROM sqlite_master WHERE type='index' AND tbl_name='indexed_model'";
            var count = (long)await cmd.ExecuteScalarAsync();
            // Esperamos pelo menos: UNIQUE em email, composto idx_full_name, anônimo em created_year.
            Assert.True(count >= 3, $"Esperava pelo menos 3 índices, obtive {count}");
        }
    }
}
