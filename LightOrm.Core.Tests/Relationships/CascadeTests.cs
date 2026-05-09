using System.Linq;
using System.Threading.Tasks;
using LightOrm.Core.Sql;
using LightOrm.Core.Tests.Models;
using LightOrm.Sqlite;
using Microsoft.Data.Sqlite;
using Xunit;

namespace LightOrm.Core.Tests
{
    public class CascadeSaveTests
    {
        private static (SqliteConnection conn, IDialect dialect) Open()
        {
            var c = new SqliteConnection("Data Source=:memory:");
            c.Open();
            return (c, new SqliteDialect());
        }

        [Fact]
        public async Task Cascade_OneToMany_inserts_children_with_parent_id()
        {
            var (conn, dialect) = Open();
            var parents = new SqlRepository<CascadeParentModel, int>(conn, dialect);
            var children = new SqlRepository<CascadeChildModel, int>(conn, dialect);
            await parents.EnsureSchemaAsync();
            await children.EnsureSchemaAsync();

            var parent = new CascadeParentModel
            {
                Name = "p1",
                Children = new[]
                {
                    new CascadeChildModel { Label = "c1" },
                    new CascadeChildModel { Label = "c2" },
                    new CascadeChildModel { Label = "c3" }
                }
            };

            await parents.SaveAsync(parent);

            // Pai tem id; filhos têm id e ParentId apontando para o pai.
            Assert.True(parent.Id > 0);
            Assert.All(parent.Children, c =>
            {
                Assert.True(c.Id > 0);
                Assert.Equal(parent.Id, c.ParentId);
            });

            // Persistido: 3 linhas em children apontando pro pai.
            var stored = await children.FindAllAsync();
            Assert.Equal(3, stored.Count);
            Assert.All(stored, c => Assert.Equal(parent.Id, c.ParentId));
        }

        [Fact]
        public async Task Cascade_with_empty_collection_is_noop()
        {
            var (conn, dialect) = Open();
            var parents = new SqlRepository<CascadeParentModel, int>(conn, dialect);
            var children = new SqlRepository<CascadeChildModel, int>(conn, dialect);
            await parents.EnsureSchemaAsync();
            await children.EnsureSchemaAsync();

            var parent = new CascadeParentModel { Name = "lonely", Children = System.Array.Empty<CascadeChildModel>() };
            await parents.SaveAsync(parent);

            Assert.Empty(await children.FindAllAsync());
        }

        [Fact]
        public async Task Cascade_with_null_collection_is_noop()
        {
            var (conn, dialect) = Open();
            var parents = new SqlRepository<CascadeParentModel, int>(conn, dialect);
            var children = new SqlRepository<CascadeChildModel, int>(conn, dialect);
            await parents.EnsureSchemaAsync();
            await children.EnsureSchemaAsync();

            var parent = new CascadeParentModel { Name = "no-children", Children = null };
            await parents.SaveAsync(parent);

            Assert.Empty(await children.FindAllAsync());
        }

        [Fact]
        public async Task Cascade_rollback_includes_children_when_child_save_fails()
        {
            var (conn, dialect) = Open();
            var parents = new SqlRepository<CascadeParentModel, int>(conn, dialect);
            var children = new SqlRepository<CascadeChildModel, int>(conn, dialect);
            await parents.EnsureSchemaAsync();
            await children.EnsureSchemaAsync();

            // Primeiro filho ok; segundo viola NOT NULL em Label.
            var parent = new CascadeParentModel
            {
                Name = "bad-batch",
                Children = new[]
                {
                    new CascadeChildModel { Label = "ok" },
                    new CascadeChildModel { Label = null } // NOT NULL violado
                }
            };

            await Assert.ThrowsAnyAsync<System.Exception>(() => parents.SaveAsync(parent));

            // Pai e filhos devem ter sido revertidos.
            Assert.Empty(await parents.FindAllAsync());
            Assert.Empty(await children.FindAllAsync());
        }

        [Fact]
        public async Task Update_with_cascade_saves_new_children_and_updates_existing()
        {
            var (conn, dialect) = Open();
            var parents = new SqlRepository<CascadeParentModel, int>(conn, dialect);
            var children = new SqlRepository<CascadeChildModel, int>(conn, dialect);
            await parents.EnsureSchemaAsync();
            await children.EnsureSchemaAsync();

            var parent = new CascadeParentModel
            {
                Name = "v1",
                Children = new[] { new CascadeChildModel { Label = "original" } }
            };
            await parents.SaveAsync(parent);
            var firstChildId = parent.Children[0].Id;

            // Update do pai com filho existente alterado + novo filho.
            parent.Name = "v2";
            parent.Children[0].Label = "alterado";
            parent.Children = parent.Children.Concat(new[]
            {
                new CascadeChildModel { Label = "novo" }
            }).ToArray();

            await parents.SaveAsync(parent);

            var allChildren = await children.FindAllAsync();
            Assert.Equal(2, allChildren.Count);
            Assert.Contains(allChildren, c => c.Id == firstChildId && c.Label == "alterado");
            Assert.Contains(allChildren, c => c.Label == "novo" && c.ParentId == parent.Id);
        }
    }
}
