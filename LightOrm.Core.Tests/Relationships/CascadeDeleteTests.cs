using System.Threading.Tasks;
using LightOrm.Core.Sql;
using LightOrm.Core.Tests.Models;
using LightOrm.Sqlite;
using Microsoft.Data.Sqlite;
using Xunit;

namespace LightOrm.Core.Tests
{
    public class CascadeDeleteTests
    {
        private static (SqliteConnection conn, IDialect dialect) Open()
        {
            var c = new SqliteConnection("Data Source=:memory:");
            c.Open();
            return (c, new SqliteDialect());
        }

        [Fact]
        public async Task Delete_parent_cascades_to_children()
        {
            var (conn, dialect) = Open();
            var parents = new SqlRepository<CascadeDeleteParentModel, int>(conn, dialect);
            var children = new SqlRepository<CascadeChildModel, int>(conn, dialect);
            await parents.EnsureSchemaAsync();
            await children.EnsureSchemaAsync();

            var p = await parents.SaveAsync(new CascadeDeleteParentModel { Name = "p" });
            await children.SaveAsync(new CascadeChildModel { Label = "c1", ParentId = p.Id });
            await children.SaveAsync(new CascadeChildModel { Label = "c2", ParentId = p.Id });
            // Filho órfão (parent_id de outro pai) — não deve ser apagado.
            await children.SaveAsync(new CascadeChildModel { Label = "outro", ParentId = 999 });

            await parents.DeleteAsync(p);

            var remaining = await children.FindAllAsync();
            Assert.Single(remaining);
            Assert.Equal("outro", remaining[0].Label);
        }

        [Fact]
        public async Task Cascade_delete_atomic_within_transaction()
        {
            var (conn, dialect) = Open();
            var parents = new SqlRepository<CascadeDeleteParentModel, int>(conn, dialect);
            var children = new SqlRepository<CascadeChildModel, int>(conn, dialect);
            await parents.EnsureSchemaAsync();
            await children.EnsureSchemaAsync();

            var p = await parents.SaveAsync(new CascadeDeleteParentModel { Name = "p" });
            await children.SaveAsync(new CascadeChildModel { Label = "c", ParentId = p.Id });

            using (var tx = conn.BeginTransaction())
            {
                var parentsTx = new SqlRepository<CascadeDeleteParentModel, int>(conn, dialect, tx);
                await parentsTx.DeleteAsync(p);
                tx.Rollback();
            }

            // Rollback restaura tudo: nem o pai nem o filho devem ter sido apagados.
            Assert.Single(await parents.FindAllAsync());
            Assert.Single(await children.FindAllAsync());
        }
    }
}
