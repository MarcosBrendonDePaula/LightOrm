using System.Threading.Tasks;
using LightOrm.Core.Sql;
using LightOrm.Core.Tests.Models;
using LightOrm.Sqlite;
using Microsoft.Data.Sqlite;
using Xunit;

namespace LightOrm.Core.Tests
{
    public class OptimisticLockingTests
    {
        private static (SqliteConnection conn, IDialect dialect) Open()
        {
            var c = new SqliteConnection("Data Source=:memory:");
            c.Open();
            return (c, new SqliteDialect());
        }

        [Fact]
        public async Task Insert_initializes_version_to_one()
        {
            var (conn, dialect) = Open();
            var repo = new SqlRepository<VersionedModel, int>(conn, dialect);
            await repo.EnsureSchemaAsync();

            var entity = new VersionedModel { Name = "first" };
            await repo.SaveAsync(entity);

            Assert.Equal(1, entity.RowVersion);
            var loaded = await repo.FindByIdAsync(entity.Id);
            Assert.Equal(1, loaded.RowVersion);
        }

        [Fact]
        public async Task Update_increments_version()
        {
            var (conn, dialect) = Open();
            var repo = new SqlRepository<VersionedModel, int>(conn, dialect);
            await repo.EnsureSchemaAsync();

            var entity = new VersionedModel { Name = "v1" };
            await repo.SaveAsync(entity);
            Assert.Equal(1, entity.RowVersion);

            entity.Name = "v2";
            await repo.SaveAsync(entity);
            Assert.Equal(2, entity.RowVersion);

            entity.Name = "v3";
            await repo.SaveAsync(entity);
            Assert.Equal(3, entity.RowVersion);

            var loaded = await repo.FindByIdAsync(entity.Id);
            Assert.Equal(3, loaded.RowVersion);
            Assert.Equal("v3", loaded.Name);
        }

        [Fact]
        public async Task Concurrent_update_throws_DbConcurrencyException()
        {
            var (conn, dialect) = Open();
            var repo = new SqlRepository<VersionedModel, int>(conn, dialect);
            await repo.EnsureSchemaAsync();

            await repo.SaveAsync(new VersionedModel { Name = "shared" });

            // Dois "leitores" pegam a mesma linha:
            var a = await repo.FindByIdAsync(1);
            var b = await repo.FindByIdAsync(1);
            Assert.Equal(1, a.RowVersion);
            Assert.Equal(1, b.RowVersion);

            // 'a' salva primeiro — sucesso, vira version 2.
            a.Name = "by-a";
            await repo.SaveAsync(a);
            Assert.Equal(2, a.RowVersion);

            // 'b' tenta salvar com version original (1) — conflito.
            b.Name = "by-b";
            await Assert.ThrowsAsync<DbConcurrencyException>(() => repo.SaveAsync(b));

            // Estado consistente: nada de 'b' foi escrito.
            var current = await repo.FindByIdAsync(1);
            Assert.Equal("by-a", current.Name);
            Assert.Equal(2, current.RowVersion);
        }

        [Fact]
        public async Task Reload_after_conflict_allows_retry()
        {
            var (conn, dialect) = Open();
            var repo = new SqlRepository<VersionedModel, int>(conn, dialect);
            await repo.EnsureSchemaAsync();

            await repo.SaveAsync(new VersionedModel { Name = "x" });

            var stale = await repo.FindByIdAsync(1);
            // Outra escrita acontece:
            var fresh = await repo.FindByIdAsync(1);
            fresh.Name = "race-winner";
            await repo.SaveAsync(fresh);

            // Tentativa stale falha:
            stale.Name = "race-loser";
            await Assert.ThrowsAsync<DbConcurrencyException>(() => repo.SaveAsync(stale));

            // Após conflito, RowVersion em 'stale' foi revertido — recarrega e tenta de novo.
            var reloaded = await repo.FindByIdAsync(1);
            reloaded.Name = "after-retry";
            await repo.SaveAsync(reloaded);

            var finalState = await repo.FindByIdAsync(1);
            Assert.Equal("after-retry", finalState.Name);
            Assert.Equal(3, finalState.RowVersion);
        }
    }
}
