using System;
using System.Threading.Tasks;
using LightOrm.Core.Attributes;
using LightOrm.Core.Models;
using LightOrm.Core.Sql;
using LightOrm.Core.Tests.Models;
using LightOrm.Sqlite;
using Microsoft.Data.Sqlite;

namespace LightOrm.Core.Tests
{
    public class VersionEdgeCaseTests
    {
        private static (SqliteConnection conn, IDialect dialect) Open()
        {
            var c = new SqliteConnection("Data Source=:memory:");
            c.Open();
            return (c, new SqliteDialect());
        }

        [Fact]
        public async Task Save_with_invalid_version_type_throws_clear_error()
        {
            var (conn, dialect) = Open();
            var repo = new SqlRepository<InvalidVersionModel, int>(conn, dialect);
            await repo.EnsureSchemaAsync();

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                repo.SaveAsync(new InvalidVersionModel { Name = "bad", RowVersion = "v1" }));

            Assert.Contains("[Version]", ex.Message);
            Assert.Contains("int ou long", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task SaveMany_rolls_back_entire_batch_when_one_update_has_stale_version()
        {
            var (conn, dialect) = Open();
            var repo = new SqlRepository<VersionedModel, int>(conn, dialect);
            await repo.EnsureSchemaAsync();

            var original = await repo.SaveAsync(new VersionedModel { Name = "seed" });
            var stale = await repo.FindByIdAsync(original.Id);
            original.Name = "fresh";
            await repo.SaveAsync(original);

            var newcomer = new VersionedModel { Name = "newcomer" };
            stale.Name = "stale-update";

            await Assert.ThrowsAsync<DbConcurrencyException>(() =>
                repo.SaveManyAsync(new[] { newcomer, stale }));

            var all = await repo.FindAllAsync();
            Assert.Single(all);
            Assert.Equal("fresh", all[0].Name);
            Assert.Equal(original.Id, all[0].Id);
        }
    }

    public class InvalidVersionModel : BaseModel<InvalidVersionModel, int>
    {
        public override string TableName => "invalid_version_model";

        [Column("name", length: 50)]
        public string Name { get; set; }

        [Column("row_version")]
        [Version]
        public string RowVersion { get; set; }
    }
}
