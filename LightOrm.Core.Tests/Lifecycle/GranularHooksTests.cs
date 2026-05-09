using System.Linq;
using System.Threading.Tasks;
using LightOrm.Core.Sql;
using LightOrm.Core.Tests.Models;
using LightOrm.Sqlite;
using Microsoft.Data.Sqlite;
using Xunit;

namespace LightOrm.Core.Tests.Lifecycle
{
    public class GranularHooksTests
    {
        private static (SqliteConnection conn, IDialect dialect) Open()
        {
            var c = new SqliteConnection("Data Source=:memory:");
            c.Open();
            return (c, new SqliteDialect());
        }

        [Fact]
        public async Task Insert_fires_BeforeCreate_and_AfterCreate_in_order()
        {
            var (conn, dialect) = Open();
            var repo = new SqlRepository<GranularHookModel, int>(conn, dialect);
            await repo.EnsureSchemaAsync();

            var entity = new GranularHookModel { Name = "x" };
            await repo.SaveAsync(entity);

            Assert.Contains("before-create", entity.Events);
            Assert.Contains("after-create", entity.Events);
            // Update hooks NÃO devem disparar em insert.
            Assert.DoesNotContain("before-update", entity.Events);
            Assert.DoesNotContain("after-update", entity.Events);
            // Validate envolve a operação.
            Assert.Contains("before-validate", entity.Events);
            Assert.Contains("after-validate", entity.Events);
            // Ordem: before-create antes de before-validate antes de after-create.
            Assert.True(entity.Events.IndexOf("before-create") < entity.Events.IndexOf("before-validate"));
            Assert.True(entity.Events.IndexOf("before-validate") < entity.Events.IndexOf("after-validate"));
            Assert.True(entity.Events.IndexOf("after-validate") < entity.Events.IndexOf("after-create"));
        }

        [Fact]
        public async Task Update_fires_BeforeUpdate_and_AfterUpdate_only()
        {
            var (conn, dialect) = Open();
            var repo = new SqlRepository<GranularHookModel, int>(conn, dialect);
            await repo.EnsureSchemaAsync();

            var entity = new GranularHookModel { Name = "v1" };
            await repo.SaveAsync(entity);
            entity.Events.Clear();

            entity.Name = "v2";
            await repo.SaveAsync(entity);

            Assert.Contains("before-update", entity.Events);
            Assert.Contains("after-update", entity.Events);
            Assert.DoesNotContain("before-create", entity.Events);
            Assert.DoesNotContain("after-create", entity.Events);
        }

        [Fact]
        public async Task CanSaveAsync_returning_false_skips_save()
        {
            var (conn, dialect) = Open();
            var repo = new SqlRepository<GranularHookModel, int>(conn, dialect);
            await repo.EnsureSchemaAsync();

            var entity = new GranularHookModel { Name = "x", BlockSave = true };
            await repo.SaveAsync(entity);

            // Hooks before disparam, after-create NÃO.
            Assert.Contains("before-create", entity.Events);
            Assert.DoesNotContain("after-create", entity.Events);
            // Validação foi pulada também.
            Assert.DoesNotContain("before-validate", entity.Events);
            // Não persistiu — tabela vazia.
            var all = await repo.FindAllAsync();
            Assert.Empty(all);
        }

        [Fact]
        public async Task CanDeleteAsync_returning_false_skips_delete()
        {
            var (conn, dialect) = Open();
            var repo = new SqlRepository<GranularHookModel, int>(conn, dialect);
            await repo.EnsureSchemaAsync();

            var entity = new GranularHookModel { Name = "x" };
            await repo.SaveAsync(entity);
            entity.Events.Clear();
            entity.BlockDelete = true;

            await repo.DeleteAsync(entity);

            // Não deletou (soft-delete também).
            var all = await repo.FindAllIncludingDeletedAsync();
            Assert.Single(all);
            Assert.Null(all[0].DeletedAt);
        }

        [Fact]
        public async Task Restore_fires_BeforeRestore_and_AfterRestore()
        {
            var (conn, dialect) = Open();
            var repo = new SqlRepository<GranularHookModel, int>(conn, dialect);
            await repo.EnsureSchemaAsync();

            var entity = new GranularHookModel { Name = "x" };
            await repo.SaveAsync(entity);
            await repo.DeleteAsync(entity);
            entity.Events.Clear();

            await repo.RestoreAsync(entity);
            Assert.Contains("before-restore", entity.Events);
            Assert.Contains("after-restore", entity.Events);
        }

        [Fact]
        public async Task Save_can_mutate_entity_via_BeforeCreate()
        {
            // Cenário concreto: hash de senha. O hook lê PlainPassword e seta
            // PasswordHash antes do INSERT.
            var (conn, dialect) = Open();
            var repo = new SqlRepository<HashedModel, int>(conn, dialect);
            await repo.EnsureSchemaAsync();

            var entity = new HashedModel { Name = "ana", PlainPassword = "secret" };
            await repo.SaveAsync(entity);
            Assert.NotNull(entity.HashedPassword);
            Assert.NotEqual("secret", entity.HashedPassword);

            var loaded = await repo.FindByIdAsync(entity.Id);
            Assert.Equal(entity.HashedPassword, loaded.HashedPassword);
        }
    }

    public class HashedModel : LightOrm.Core.Models.BaseModel<HashedModel, int>
    {
        public override string TableName => "hashed_model";

        [LightOrm.Core.Attributes.Column("name", length: 50)]
        public string Name { get; set; }

        [LightOrm.Core.Attributes.Column("password_hash", length: 100)]
        public string HashedPassword { get; set; }

        // Não-coluna — fonte de dado pro hook.
        public string PlainPassword { get; set; }

        protected internal override void OnBeforeCreate()
        {
            if (!string.IsNullOrEmpty(PlainPassword))
                HashedPassword = "hashed_" + PlainPassword.Length + "_" + PlainPassword.GetHashCode();
        }
    }
}
