using System.Threading.Tasks;
using LightOrm.Core.Sql;
using LightOrm.Core.Tests.Models;
using LightOrm.Sqlite;
using Microsoft.Data.Sqlite;
using Xunit;

namespace LightOrm.Core.Tests
{
    public class EagerMultiLevelTests
    {
        private static (SqliteConnection conn, IDialect dialect) Open()
        {
            var c = new SqliteConnection("Data Source=:memory:");
            c.Open();
            return (c, new SqliteDialect());
        }

        [Fact]
        public async Task IncludeRelated_loads_three_levels_deep()
        {
            var (conn, dialect) = Open();
            var grands = new SqlRepository<GrandparentModel, int>(conn, dialect);
            var mids = new SqlRepository<MidParentModel, int>(conn, dialect);
            var leaves = new SqlRepository<LeafModel, int>(conn, dialect);
            await grands.EnsureSchemaAsync();
            await mids.EnsureSchemaAsync();
            await leaves.EnsureSchemaAsync();

            var g = await grands.SaveAsync(new GrandparentModel { Name = "g" });
            var m1 = await mids.SaveAsync(new MidParentModel { Label = "m1", GrandparentId = g.Id });
            var m2 = await mids.SaveAsync(new MidParentModel { Label = "m2", GrandparentId = g.Id });
            await leaves.SaveAsync(new LeafModel { Value = "l1a", MidparentId = m1.Id });
            await leaves.SaveAsync(new LeafModel { Value = "l1b", MidparentId = m1.Id });
            await leaves.SaveAsync(new LeafModel { Value = "l2a", MidparentId = m2.Id });

            var loaded = await grands.FindByIdAsync(g.Id, includeRelated: true);
            Assert.NotNull(loaded.Mids);
            Assert.Equal(2, loaded.Mids.Length);
            Assert.All(loaded.Mids, mid =>
            {
                Assert.NotNull(mid.Leaves);
                Assert.True(mid.Leaves.Length > 0);
            });

            // m1 deve ter 2 leaves, m2 deve ter 1.
            var m1Loaded = System.Array.Find(loaded.Mids, x => x.Label == "m1");
            var m2Loaded = System.Array.Find(loaded.Mids, x => x.Label == "m2");
            Assert.Equal(2, m1Loaded.Leaves.Length);
            Assert.Single(m2Loaded.Leaves);
        }

        [Fact]
        public async Task IncludeRelated_findall_loads_recursively()
        {
            var (conn, dialect) = Open();
            var grands = new SqlRepository<GrandparentModel, int>(conn, dialect);
            var mids = new SqlRepository<MidParentModel, int>(conn, dialect);
            var leaves = new SqlRepository<LeafModel, int>(conn, dialect);
            await grands.EnsureSchemaAsync();
            await mids.EnsureSchemaAsync();
            await leaves.EnsureSchemaAsync();

            for (int i = 0; i < 3; i++)
            {
                var g = await grands.SaveAsync(new GrandparentModel { Name = $"g{i}" });
                var m = await mids.SaveAsync(new MidParentModel { Label = $"m{i}", GrandparentId = g.Id });
                await leaves.SaveAsync(new LeafModel { Value = $"l{i}", MidparentId = m.Id });
            }

            var all = await grands.FindAllAsync(includeRelated: true);
            Assert.Equal(3, all.Count);
            Assert.All(all, g =>
            {
                Assert.Single(g.Mids);
                Assert.Single(g.Mids[0].Leaves);
            });
        }
    }
}
