using System.Threading.Tasks;
using LightOrm.Core.Models;
using LightOrm.Core.Sql;
using LightOrm.Core.Tests.Models;
using LightOrm.Sqlite;
using Microsoft.Data.Sqlite;
using Xunit;

namespace LightOrm.Core.Tests.Querying
{
    public class ScopeTests
    {
        private static (SqliteConnection conn, IDialect dialect) Open()
        {
            var c = new SqliteConnection("Data Source=:memory:");
            c.Open();
            return (c, new SqliteDialect());
        }

        private async Task<SqlRepository<ScopedModel, int>> Seed()
        {
            var (conn, dialect) = Open();
            var repo = new SqlRepository<ScopedModel, int>(conn, dialect);
            await repo.EnsureSchemaAsync();
            await repo.SaveAsync(new ScopedModel { Name = "alpha", Active = true,  Priority = 1 });
            await repo.SaveAsync(new ScopedModel { Name = "bravo", Active = true,  Priority = 7 });
            await repo.SaveAsync(new ScopedModel { Name = "delta", Active = false, Priority = 9 });
            await repo.SaveAsync(new ScopedModel { Name = "charlie", Active = true, Priority = 5 });
            return repo;
        }

        [Fact]
        public async Task Scope_applies_predicate()
        {
            var repo = await Seed();
            var actives = await repo.Scope("active").ToListAsync();
            Assert.Equal(3, actives.Count);
            Assert.All(actives, m => Assert.True(m.Active));
        }

        [Fact]
        public async Task Scopes_compose_with_AND_via_chaining()
        {
            var repo = await Seed();
            var result = await repo.Scope("active").Scope("highPriority").ToListAsync();
            // active=true E priority>=5 → bravo (7), charlie (5).
            Assert.Equal(2, result.Count);
        }

        [Fact]
        public async Task Scope_followed_by_Where_combines()
        {
            var repo = await Seed();
            var result = await repo.Scope("active")
                .Where(nameof(ScopedModel.Name), "bravo")
                .ToListAsync();
            Assert.Single(result);
            Assert.Equal("bravo", result[0].Name);
        }

        [Fact]
        public async Task Scope_with_OrderBy_inside()
        {
            var repo = await Seed();
            var result = await repo.Scope("byName").ToListAsync();
            Assert.Equal("alpha", result[0].Name);
            Assert.Equal("bravo", result[1].Name);
            Assert.Equal("charlie", result[2].Name);
            Assert.Equal("delta", result[3].Name);
        }

        [Fact]
        public async Task Unknown_scope_throws()
        {
            var repo = await Seed();
            await Assert.ThrowsAsync<System.ArgumentException>(() =>
                repo.Scope("inexistente").ToListAsync());
        }
    }
}
