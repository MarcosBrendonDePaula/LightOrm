using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LightOrm.Core.Sql;
using LightOrm.Core.Tests.Models;
using LightOrm.Sqlite;
using Microsoft.Data.Sqlite;

namespace LightOrm.Core.Tests
{
    public class RepositoryContractTests
    {
        private static SqlRepository<TypesModel, int> CreateRepo()
        {
            var conn = new SqliteConnection("Data Source=:memory:");
            conn.Open();
            var repo = new SqlRepository<TypesModel, int>(conn, new SqliteDialect());
            repo.EnsureSchemaAsync().GetAwaiter().GetResult();
            return repo;
        }

        [Fact]
        public async Task SaveMany_with_null_throws()
        {
            var repo = CreateRepo();

            await Assert.ThrowsAsync<ArgumentNullException>(() => repo.SaveManyAsync(null));
        }

        [Fact]
        public async Task SaveMany_with_empty_collection_returns_empty_and_does_not_insert()
        {
            var repo = CreateRepo();

            var result = await repo.SaveManyAsync(Array.Empty<TypesModel>());

            Assert.Empty(result);
            Assert.Empty(await repo.FindAllAsync());
        }

        [Fact]
        public async Task Upsert_with_null_entity_throws()
        {
            var repo = CreateRepo();

            await Assert.ThrowsAsync<ArgumentNullException>(() => repo.UpsertAsync(null));
        }

        [Fact]
        public async Task FindOrCreate_with_null_filter_throws()
        {
            var repo = CreateRepo();
            var defaults = new TypesModel
            {
                Name = "x",
                GuidValue = Guid.NewGuid(),
                DecimalValue = 1m,
                DateValue = DateTime.UtcNow
            };

            await Assert.ThrowsAsync<ArgumentNullException>(() => repo.FindOrCreateAsync(null, defaults));
        }

        [Fact]
        public async Task FindOrCreate_with_null_defaults_throws()
        {
            var repo = CreateRepo();

            await Assert.ThrowsAsync<ArgumentNullException>(() => repo.FindOrCreateAsync(q => q.Where(nameof(TypesModel.Name), "x"), null));
        }

        [Fact]
        public async Task Raw_with_blank_sql_throws()
        {
            var repo = CreateRepo();

            await Assert.ThrowsAsync<ArgumentException>(() => repo.RawAsync("   "));
        }

        [Fact]
        public async Task Restore_on_non_soft_delete_model_throws()
        {
            var repo = CreateRepo();
            var entity = new TypesModel
            {
                Name = "x",
                GuidValue = Guid.NewGuid(),
                DecimalValue = 1m,
                DateValue = DateTime.UtcNow
            };

            await repo.SaveAsync(entity);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => repo.RestoreAsync(entity));
            Assert.Contains("SoftDelete", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
    }
}
