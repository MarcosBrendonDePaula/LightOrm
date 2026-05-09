using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LightOrm.Core.Sql;
using LightOrm.Core.Tests.Models;
using LightOrm.Sqlite;
using Microsoft.Data.Sqlite;

namespace LightOrm.Core.Tests
{
    public class BulkMutationEdgeCaseTests
    {
        private static async Task<SqlRepository<TypesModel, int>> SeedAsync()
        {
            var conn = new SqliteConnection("Data Source=:memory:");
            conn.Open();
            var repo = new SqlRepository<TypesModel, int>(conn, new SqliteDialect());
            await repo.EnsureSchemaAsync();
            await repo.SaveAsync(new TypesModel { Name = "a", GuidValue = Guid.NewGuid(), DecimalValue = 1m, DateValue = DateTime.UtcNow, NullableInt = 1 });
            await repo.SaveAsync(new TypesModel { Name = "b", GuidValue = Guid.NewGuid(), DecimalValue = 2m, DateValue = DateTime.UtcNow, NullableInt = 2 });
            await repo.SaveAsync(new TypesModel { Name = "c", GuidValue = Guid.NewGuid(), DecimalValue = 3m, DateValue = DateTime.UtcNow, NullableInt = 3 });
            return repo;
        }

        [Fact]
        public async Task Bulk_update_can_set_nullable_column_to_null()
        {
            var repo = await SeedAsync();

            var affected = await repo.Query()
                .Where(nameof(TypesModel.DecimalValue), ">=", 2m)
                .UpdateAsync(new Dictionary<string, object> { [nameof(TypesModel.NullableInt)] = null });

            Assert.Equal(2, affected);
            var all = await repo.FindAllAsync();
            Assert.Equal(1, all.Find(x => x.Name == "a").NullableInt);
            Assert.Null(all.Find(x => x.Name == "b").NullableInt);
            Assert.Null(all.Find(x => x.Name == "c").NullableInt);
        }

        [Fact]
        public async Task Bulk_delete_with_where_any_deletes_only_matching_or_branches()
        {
            var repo = await SeedAsync();

            var affected = await repo.Query()
                .WhereAny(
                    (nameof(TypesModel.Name), "=", "a"),
                    (nameof(TypesModel.Name), "=", "c"))
                .DeleteAsync();

            Assert.Equal(2, affected);
            var remaining = await repo.FindAllAsync();
            Assert.Single(remaining);
            Assert.Equal("b", remaining[0].Name);
        }
    }
}
