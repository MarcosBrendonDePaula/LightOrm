using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LightOrm.Core.Sql;
using LightOrm.Core.Tests.Models;
using LightOrm.Sqlite;
using Microsoft.Data.Sqlite;

namespace LightOrm.Core.Tests
{
    public class QueryContractTests
    {
        private static async Task<SqlRepository<TypesModel, int>> SeedAsync()
        {
            var conn = new SqliteConnection("Data Source=:memory:");
            conn.Open();
            var repo = new SqlRepository<TypesModel, int>(conn, new SqliteDialect());
            await repo.EnsureSchemaAsync();
            await repo.SaveAsync(new TypesModel
            {
                Name = "one",
                GuidValue = Guid.NewGuid(),
                DecimalValue = 10m,
                DateValue = new DateTime(2026, 5, 8, 12, 0, 0, DateTimeKind.Utc)
            });
            await repo.SaveAsync(new TypesModel
            {
                Name = "two",
                GuidValue = Guid.NewGuid(),
                DecimalValue = 20m,
                DateValue = new DateTime(2026, 5, 9, 12, 0, 0, DateTimeKind.Utc)
            });
            return repo;
        }

        [Fact]
        public async Task Take_with_negative_value_throws()
        {
            var repo = await SeedAsync();

            Assert.Throws<ArgumentOutOfRangeException>(() => repo.Query().Take(-1));
        }

        [Fact]
        public async Task Skip_with_negative_value_throws()
        {
            var repo = await SeedAsync();

            Assert.Throws<ArgumentOutOfRangeException>(() => repo.Query().Skip(-1));
        }

        [Fact]
        public async Task Update_with_empty_set_throws()
        {
            var repo = await SeedAsync();

            await Assert.ThrowsAsync<ArgumentException>(() =>
                repo.Query().Where(nameof(TypesModel.Name), "one").UpdateAsync(new Dictionary<string, object>()));
        }

        [Fact]
        public async Task GroupBy_respects_filter_and_keeps_null_key_when_present()
        {
            var conn = new SqliteConnection("Data Source=:memory:");
            conn.Open();
            var repo = new SqlRepository<TypesModel, int>(conn, new SqliteDialect());
            await repo.EnsureSchemaAsync();

            await repo.SaveAsync(new TypesModel { Name = "a", GuidValue = Guid.NewGuid(), DecimalValue = 1m, DateValue = DateTime.UtcNow, NullableInt = 1 });
            await repo.SaveAsync(new TypesModel { Name = "b", GuidValue = Guid.NewGuid(), DecimalValue = 2m, DateValue = DateTime.UtcNow, NullableInt = 1 });
            await repo.SaveAsync(new TypesModel { Name = "c", GuidValue = Guid.NewGuid(), DecimalValue = 3m, DateValue = DateTime.UtcNow, NullableInt = null });

            var groups = await repo.Query()
                .Where(nameof(TypesModel.DecimalValue), ">", 1m)
                .GroupByAsync(nameof(TypesModel.NullableInt));

            Assert.Equal(2, groups.Count);
            Assert.Equal(1, groups.Find(g => Equals(g.key?.ToString(), "1")).count);
            Assert.Equal(1, groups.Find(g => g.key == null).count);
        }

        [Fact]
        public async Task Min_and_max_on_datetime_return_ticks()
        {
            var repo = await SeedAsync();

            var min = await repo.Query().MinAsync(nameof(TypesModel.DateValue));
            var max = await repo.Query().MaxAsync(nameof(TypesModel.DateValue));

            Assert.Equal(new DateTime(2026, 5, 8, 12, 0, 0, DateTimeKind.Utc).Ticks, min);
            Assert.Equal(new DateTime(2026, 5, 9, 12, 0, 0, DateTimeKind.Utc).Ticks, max);
        }
    }
}
