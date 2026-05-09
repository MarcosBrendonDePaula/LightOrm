using System.Linq;
using System.Threading.Tasks;
using LightOrm.Core.Sql;
using LightOrm.Core.Tests.Models;
using LightOrm.Core.Validation;
using LightOrm.Sqlite;
using Microsoft.Data.Sqlite;
using Xunit;

namespace LightOrm.Core.Tests
{
    public class HooksTests
    {
        private static (SqliteConnection conn, IDialect dialect) Open()
        {
            var c = new SqliteConnection("Data Source=:memory:");
            c.Open();
            return (c, new SqliteDialect());
        }

        [Fact]
        public async Task Insert_fires_before_and_after_save_hooks()
        {
            var (conn, dialect) = Open();
            var repo = new SqlRepository<HookTrackingModel, int>(conn, dialect);
            await repo.EnsureSchemaAsync();

            var entity = new HookTrackingModel { Name = "x" };
            await repo.SaveAsync(entity);

            Assert.Contains("before-insert", entity.Events);
            Assert.Contains("after-insert", entity.Events);
            Assert.True(entity.Events.IndexOf("before-insert") < entity.Events.IndexOf("after-insert"));
        }

        [Fact]
        public async Task Update_fires_before_and_after_update_hooks()
        {
            var (conn, dialect) = Open();
            var repo = new SqlRepository<HookTrackingModel, int>(conn, dialect);
            await repo.EnsureSchemaAsync();

            var entity = new HookTrackingModel { Name = "v1" };
            await repo.SaveAsync(entity);
            entity.Events.Clear();

            entity.Name = "v2";
            await repo.SaveAsync(entity);

            Assert.Contains("before-update", entity.Events);
            Assert.Contains("after-update", entity.Events);
        }

        [Fact]
        public async Task Delete_fires_before_and_after_delete_hooks()
        {
            var (conn, dialect) = Open();
            var repo = new SqlRepository<HookTrackingModel, int>(conn, dialect);
            await repo.EnsureSchemaAsync();

            var entity = new HookTrackingModel { Name = "x" };
            await repo.SaveAsync(entity);
            entity.Events.Clear();

            await repo.DeleteAsync(entity);

            Assert.Contains("before-delete", entity.Events);
            Assert.Contains("after-delete", entity.Events);
        }

        [Fact]
        public async Task FindById_fires_after_load_hook()
        {
            var (conn, dialect) = Open();
            var repo = new SqlRepository<HookTrackingModel, int>(conn, dialect);
            await repo.EnsureSchemaAsync();

            var seed = new HookTrackingModel { Name = "x" };
            await repo.SaveAsync(seed);

            var loaded = await repo.FindByIdAsync(seed.Id);
            Assert.Contains("after-load", loaded.Events);
        }

        [Fact]
        public async Task FindAll_fires_after_load_for_each()
        {
            var (conn, dialect) = Open();
            var repo = new SqlRepository<HookTrackingModel, int>(conn, dialect);
            await repo.EnsureSchemaAsync();

            await repo.SaveAsync(new HookTrackingModel { Name = "a" });
            await repo.SaveAsync(new HookTrackingModel { Name = "b" });

            var all = await repo.FindAllAsync();
            Assert.All(all, e => Assert.Contains("after-load", e.Events));
        }

        [Fact]
        public async Task Query_ToList_fires_after_load_for_each()
        {
            var (conn, dialect) = Open();
            var repo = new SqlRepository<HookTrackingModel, int>(conn, dialect);
            await repo.EnsureSchemaAsync();

            await repo.SaveAsync(new HookTrackingModel { Name = "a" });
            await repo.SaveAsync(new HookTrackingModel { Name = "b" });

            var found = await repo.Query()
                .Where(nameof(HookTrackingModel.Name), "a")
                .ToListAsync();
            Assert.Single(found);
            Assert.Contains("after-load", found[0].Events);
        }

        [Fact]
        public async Task SaveMany_fires_before_and_after_save_hooks_for_each_entity()
        {
            var (conn, dialect) = Open();
            var repo = new SqlRepository<HookTrackingModel, int>(conn, dialect);
            await repo.EnsureSchemaAsync();

            var batch = new[]
            {
                new HookTrackingModel { Name = "a" },
                new HookTrackingModel { Name = "b" }
            };

            await repo.SaveManyAsync(batch);

            Assert.All(batch, entity =>
            {
                Assert.Contains("before-insert", entity.Events);
                Assert.Contains("after-insert", entity.Events);
                Assert.True(entity.Events.IndexOf("before-insert") < entity.Events.IndexOf("after-insert"));
            });
        }
    }

    public class ValidationTests
    {
        private static SqlRepository<ValidatedModel, int> Repo()
        {
            var c = new SqliteConnection("Data Source=:memory:");
            c.Open();
            var repo = new SqlRepository<ValidatedModel, int>(c, new SqliteDialect());
            repo.EnsureSchemaAsync().GetAwaiter().GetResult();
            return repo;
        }

        [Fact]
        public async Task Required_blocks_null_or_empty()
        {
            var repo = Repo();
            var ex = await Assert.ThrowsAsync<ValidationException>(() =>
                repo.SaveAsync(new ValidatedModel { Email = null, Nickname = "abc", Age = 30 }));
            Assert.Contains(ex.Errors, e => e.PropertyName == "Email" && e.Message.Contains("obrigat"));
        }

        [Fact]
        public async Task RegEx_blocks_malformed_email()
        {
            var repo = Repo();
            var ex = await Assert.ThrowsAsync<ValidationException>(() =>
                repo.SaveAsync(new ValidatedModel { Email = "not-an-email", Nickname = "abc", Age = 30 }));
            Assert.Contains(ex.Errors, e => e.PropertyName == "Email");
        }

        [Fact]
        public async Task MinLength_and_MaxLength_apply()
        {
            var repo = Repo();
            var tooShort = await Assert.ThrowsAsync<ValidationException>(() =>
                repo.SaveAsync(new ValidatedModel { Email = "a@b.com", Nickname = "ab", Age = 30 }));
            Assert.Contains(tooShort.Errors, e => e.PropertyName == "Nickname" && e.Message.Contains("m"));

            var tooLong = await Assert.ThrowsAsync<ValidationException>(() =>
                repo.SaveAsync(new ValidatedModel { Email = "a@b.com", Nickname = new string('x', 30), Age = 30 }));
            Assert.Contains(tooLong.Errors, e => e.PropertyName == "Nickname" && e.Message.Contains("m"));
        }

        [Fact]
        public async Task Range_applies()
        {
            var repo = Repo();
            var ex = await Assert.ThrowsAsync<ValidationException>(() =>
                repo.SaveAsync(new ValidatedModel { Email = "a@b.com", Nickname = "abc", Age = 200 }));
            Assert.Contains(ex.Errors, e => e.PropertyName == "Age");
        }

        [Fact]
        public async Task Valid_entity_passes()
        {
            var repo = Repo();
            var v = new ValidatedModel { Email = "ana@x.com", Nickname = "ana", Age = 30 };
            await repo.SaveAsync(v);
            Assert.True(v.Id > 0);
        }

        [Fact]
        public async Task Multiple_errors_are_aggregated()
        {
            var repo = Repo();
            var ex = await Assert.ThrowsAsync<ValidationException>(() =>
                repo.SaveAsync(new ValidatedModel { Email = null, Nickname = "ab", Age = 999 }));
            Assert.True(ex.Errors.Count >= 3, $"Esperava >=3 erros, recebi {ex.Errors.Count}");
        }

        [Fact]
        public async Task SaveMany_is_atomic_when_one_entity_is_invalid()
        {
            var repo = Repo();

            await Assert.ThrowsAsync<ValidationException>(() =>
                repo.SaveManyAsync(new[]
                {
                    new ValidatedModel { Email = "ok@x.com", Nickname = "valid", Age = 30 },
                    new ValidatedModel { Email = "bad", Nickname = "ab", Age = 999 }
                }));

            var all = await repo.FindAllAsync();
            Assert.Empty(all);
        }

        [Fact]
        public async Task SaveMany_applies_cascade_saves_for_each_root_entity()
        {
            var (conn, dialect) = Open();
            var parents = new SqlRepository<CascadeParentModel, int>(conn, dialect);
            var children = new SqlRepository<CascadeChildModel, int>(conn, dialect);
            await parents.EnsureSchemaAsync();
            await children.EnsureSchemaAsync();

            await parents.SaveManyAsync(new[]
            {
                new CascadeParentModel
                {
                    Name = "p1",
                    Children = new[] { new CascadeChildModel { Label = "c1" } }
                },
                new CascadeParentModel
                {
                    Name = "p2",
                    Children = new[] { new CascadeChildModel { Label = "c2" } }
                }
            });

            var loadedParents = await parents.FindAllAsync(includeRelated: true);
            Assert.Equal(2, loadedParents.Count);
            Assert.All(loadedParents, p => Assert.Single(p.Children));

            var loadedChildren = await children.FindAllAsync();
            Assert.Equal(2, loadedChildren.Count);
            Assert.All(loadedChildren, c => Assert.True(c.ParentId > 0));
        }

        private static (SqliteConnection conn, IDialect dialect) Open()
        {
            var c = new SqliteConnection("Data Source=:memory:");
            c.Open();
            return (c, new SqliteDialect());
        }
    }
}
