using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LightOrm.Core.Sql;
using LightOrm.Core.Tests.Models;
using LightOrm.Sqlite;
using Microsoft.Data.Sqlite;
using Xunit;

namespace LightOrm.Core.Tests
{
    public class BulkOperationsTests
    {
        private static (SqliteConnection conn, IDialect dialect) Open()
        {
            var c = new SqliteConnection("Data Source=:memory:");
            c.Open();
            return (c, new SqliteDialect());
        }

        private async Task<SqlRepository<TypesModel, int>> Seed()
        {
            var (conn, dialect) = Open();
            var repo = new SqlRepository<TypesModel, int>(conn, dialect);
            await repo.EnsureSchemaAsync();
            for (int i = 1; i <= 5; i++)
            {
                await repo.SaveAsync(new TypesModel
                {
                    Name = $"item-{i}",
                    GuidValue = Guid.NewGuid(),
                    DecimalValue = i,
                    DateValue = DateTime.UtcNow,
                    NullableInt = null
                });
            }
            return repo;
        }

        [Fact]
        public async Task Bulk_update_changes_only_matching_rows()
        {
            var repo = await Seed();
            var affected = await repo.Query()
                .Where(nameof(TypesModel.DecimalValue), ">=", 3m)
                .UpdateAsync(new Dictionary<string, object>
                {
                    [nameof(TypesModel.Name)] = "renamed"
                });
            Assert.Equal(3, affected);

            var renamed = await repo.Query()
                .Where(nameof(TypesModel.Name), "renamed")
                .ToListAsync();
            Assert.Equal(3, renamed.Count);

            var unchanged = await repo.Query()
                .Where(nameof(TypesModel.DecimalValue), "<", 3m)
                .ToListAsync();
            Assert.All(unchanged, e => Assert.StartsWith("item-", e.Name));
        }

        [Fact]
        public async Task Bulk_update_with_invalid_property_throws()
        {
            var repo = await Seed();
            await Assert.ThrowsAsync<ArgumentException>(() =>
                repo.Query().UpdateAsync(new Dictionary<string, object>
                {
                    ["PropQueNaoExiste"] = "x"
                }));
        }

        [Fact]
        public async Task Bulk_delete_removes_only_matching_rows()
        {
            var repo = await Seed();
            var deleted = await repo.Query()
                .Where(nameof(TypesModel.DecimalValue), "<", 3m)
                .DeleteAsync();
            Assert.Equal(2, deleted);

            var remaining = await repo.FindAllAsync();
            Assert.Equal(3, remaining.Count);
            Assert.All(remaining, e => Assert.True(e.DecimalValue >= 3m));
        }

        [Fact]
        public async Task Bulk_delete_on_soft_deleted_marks_as_deleted()
        {
            var (conn, dialect) = Open();
            var repo = new SqlRepository<SoftDeletedModel, int>(conn, dialect);
            await repo.EnsureSchemaAsync();
            for (int i = 0; i < 5; i++)
                await repo.SaveAsync(new SoftDeletedModel { Name = i % 2 == 0 ? "even" : "odd" });

            var affected = await repo.Query()
                .Where(nameof(SoftDeletedModel.Name), "even")
                .DeleteAsync();
            Assert.Equal(3, affected);

            // Visíveis: só os "odd".
            var visible = await repo.FindAllAsync();
            Assert.Equal(2, visible.Count);
            Assert.All(visible, x => Assert.Equal("odd", x.Name));

            // Os "even" estão lá com DeletedAt.
            var all = await repo.FindAllIncludingDeletedAsync();
            Assert.Equal(5, all.Count);
        }
    }

    public class RawQueryTests
    {
        private static (SqliteConnection conn, IDialect dialect) Open()
        {
            var c = new SqliteConnection("Data Source=:memory:");
            c.Open();
            return (c, new SqliteDialect());
        }

        [Fact]
        public async Task Raw_query_materializes_into_T()
        {
            var (conn, dialect) = Open();
            var repo = new SqlRepository<TypesModel, int>(conn, dialect);
            await repo.EnsureSchemaAsync();

            for (int i = 1; i <= 3; i++)
                await repo.SaveAsync(new TypesModel
                {
                    Name = $"raw-{i}",
                    GuidValue = Guid.NewGuid(),
                    DecimalValue = i,
                    DateValue = DateTime.UtcNow
                });

            var rows = await repo.RawAsync(
                "SELECT * FROM types_model WHERE name = @name",
                new Dictionary<string, object> { ["name"] = "raw-2" });

            Assert.Single(rows);
            Assert.Equal("raw-2", rows[0].Name);
        }

        [Fact]
        public async Task Raw_query_without_params_works()
        {
            var (conn, dialect) = Open();
            var repo = new SqlRepository<TypesModel, int>(conn, dialect);
            await repo.EnsureSchemaAsync();
            await repo.SaveAsync(new TypesModel { Name = "x", GuidValue = Guid.NewGuid(), DecimalValue = 1, DateValue = DateTime.UtcNow });

            var rows = await repo.RawAsync("SELECT * FROM types_model");
            Assert.Single(rows);
        }
    }

    public class FindOrCreateTests
    {
        private static (SqliteConnection conn, IDialect dialect) Open()
        {
            var c = new SqliteConnection("Data Source=:memory:");
            c.Open();
            return (c, new SqliteDialect());
        }

        [Fact]
        public async Task FindOrCreate_creates_when_missing()
        {
            var (conn, dialect) = Open();
            var repo = new SqlRepository<TypesModel, int>(conn, dialect);
            await repo.EnsureSchemaAsync();

            var (entity, created) = await repo.FindOrCreateAsync(
                q => q.Where(nameof(TypesModel.Name), "novo"),
                new TypesModel
                {
                    Name = "novo",
                    GuidValue = Guid.NewGuid(),
                    DecimalValue = 1,
                    DateValue = DateTime.UtcNow
                });

            Assert.True(created);
            Assert.True(entity.Id > 0);
            Assert.Equal("novo", entity.Name);
        }

        [Fact]
        public async Task FindOrCreate_returns_existing_without_creating()
        {
            var (conn, dialect) = Open();
            var repo = new SqlRepository<TypesModel, int>(conn, dialect);
            await repo.EnsureSchemaAsync();

            var seed = new TypesModel { Name = "existe", GuidValue = Guid.NewGuid(), DecimalValue = 1, DateValue = DateTime.UtcNow };
            await repo.SaveAsync(seed);

            var (entity, created) = await repo.FindOrCreateAsync(
                q => q.Where(nameof(TypesModel.Name), "existe"),
                new TypesModel { Name = "outro", GuidValue = Guid.NewGuid(), DecimalValue = 2, DateValue = DateTime.UtcNow });

            Assert.False(created);
            Assert.Equal(seed.Id, entity.Id);
            Assert.Equal("existe", entity.Name);

            var all = await repo.FindAllAsync();
            Assert.Single(all);
        }
    }
}
