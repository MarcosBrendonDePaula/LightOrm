using System.Threading.Tasks;
using LightOrm.Core.Sql;
using LightOrm.Core.Tests.Models;
using LightOrm.Sqlite;
using Microsoft.Data.Sqlite;
using Xunit;

namespace LightOrm.Core.Tests
{
    public class SoftDeleteTests
    {
        private static (SqliteConnection conn, IDialect dialect) Open()
        {
            var c = new SqliteConnection("Data Source=:memory:");
            c.Open();
            return (c, new SqliteDialect());
        }

        [Fact]
        public async Task Delete_marks_deletedAt_and_does_not_remove_row()
        {
            var (conn, dialect) = Open();
            var repo = new SqlRepository<SoftDeletedModel, int>(conn, dialect);
            await repo.EnsureSchemaAsync();

            var entity = new SoftDeletedModel { Name = "x" };
            await repo.SaveAsync(entity);
            await repo.DeleteAsync(entity);

            // Findbypadrão: não acha.
            Assert.Null(await repo.FindByIdAsync(entity.Id));

            // FindByIncludingDeleted: acha e tem DeletedAt populado.
            var raw = await repo.FindByIdIncludingDeletedAsync(entity.Id);
            Assert.NotNull(raw);
            Assert.NotNull(raw.DeletedAt);
        }

        [Fact]
        public async Task FindAll_default_filters_deleted()
        {
            var (conn, dialect) = Open();
            var repo = new SqlRepository<SoftDeletedModel, int>(conn, dialect);
            await repo.EnsureSchemaAsync();

            var a = new SoftDeletedModel { Name = "a" };
            var b = new SoftDeletedModel { Name = "b" };
            await repo.SaveAsync(a);
            await repo.SaveAsync(b);
            await repo.DeleteAsync(b);

            var visible = await repo.FindAllAsync();
            Assert.Single(visible);
            Assert.Equal("a", visible[0].Name);

            var all = await repo.FindAllIncludingDeletedAsync();
            Assert.Equal(2, all.Count);
        }

        [Fact]
        public async Task Query_default_filters_deleted()
        {
            var (conn, dialect) = Open();
            var repo = new SqlRepository<SoftDeletedModel, int>(conn, dialect);
            await repo.EnsureSchemaAsync();

            var a = new SoftDeletedModel { Name = "alive" };
            var b = new SoftDeletedModel { Name = "dead" };
            await repo.SaveAsync(a);
            await repo.SaveAsync(b);
            await repo.DeleteAsync(b);

            var visible = await repo.Query().ToListAsync();
            Assert.Single(visible);

            var withFilter = await repo.Query()
                .Where(nameof(SoftDeletedModel.Name), "alive")
                .ToListAsync();
            Assert.Single(withFilter);

            var matchesDead = await repo.Query()
                .Where(nameof(SoftDeletedModel.Name), "dead")
                .ToListAsync();
            Assert.Empty(matchesDead);
        }

        [Fact]
        public async Task Restore_zeroes_deletedAt_and_makes_record_visible()
        {
            var (conn, dialect) = Open();
            var repo = new SqlRepository<SoftDeletedModel, int>(conn, dialect);
            await repo.EnsureSchemaAsync();

            var entity = new SoftDeletedModel { Name = "x" };
            await repo.SaveAsync(entity);
            await repo.DeleteAsync(entity);
            Assert.NotNull(entity.DeletedAt);

            await repo.RestoreAsync(entity);
            Assert.Null(entity.DeletedAt);

            var loaded = await repo.FindByIdAsync(entity.Id);
            Assert.NotNull(loaded);
            Assert.Null(loaded.DeletedAt);
        }

        [Fact]
        public async Task Save_after_restore_works()
        {
            var (conn, dialect) = Open();
            var repo = new SqlRepository<SoftDeletedModel, int>(conn, dialect);
            await repo.EnsureSchemaAsync();

            var entity = new SoftDeletedModel { Name = "v1" };
            await repo.SaveAsync(entity);
            await repo.DeleteAsync(entity);
            await repo.RestoreAsync(entity);

            entity.Name = "v2";
            await repo.SaveAsync(entity);

            var loaded = await repo.FindByIdAsync(entity.Id);
            Assert.Equal("v2", loaded.Name);
        }
    }
}
