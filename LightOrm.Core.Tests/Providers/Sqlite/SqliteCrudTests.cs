using System.Threading.Tasks;
using LightOrm.Core.Sql;
using LightOrm.Core.Tests.Models;
using LightOrm.Sqlite;
using Microsoft.Data.Sqlite;
using Xunit;

namespace LightOrm.Core.Tests
{
    public class SqliteCrudTests
    {
        private static SqliteConnection NewInMemory()
        {
            var conn = new SqliteConnection("Data Source=:memory:");
            conn.Open();
            return conn;
        }

        [Fact]
        public async Task CanCreateAndRoundtripUser()
        {
            using var conn = NewInMemory();
            var repo = new SqlRepository<TestUserModel, int>(conn, new SqliteDialect());
            await repo.EnsureSchemaAsync();

            var user = new TestUserModel
            {
                UserName = "Sqlite User",
                EmailAddress = "s@x.com",
                IsActive = true
            };
            await repo.SaveAsync(user);

            Assert.True(user.Id > 0);
            var loaded = await repo.FindByIdAsync(user.Id);
            Assert.NotNull(loaded);
            Assert.Equal("Sqlite User", loaded.UserName);
            Assert.True(loaded.IsActive);
        }

        [Fact]
        public async Task CanUpdateAndDelete()
        {
            using var conn = NewInMemory();
            var repo = new SqlRepository<TestUserModel, int>(conn, new SqliteDialect());
            await repo.EnsureSchemaAsync();

            var user = new TestUserModel { UserName = "Old", EmailAddress = "o@x.com", IsActive = false };
            await repo.SaveAsync(user);
            user.UserName = "New";
            user.IsActive = true;
            await repo.SaveAsync(user);

            var loaded = await repo.FindByIdAsync(user.Id);
            Assert.Equal("New", loaded.UserName);
            Assert.True(loaded.IsActive);

            await repo.DeleteAsync(loaded);
            Assert.Null(await repo.FindByIdAsync(user.Id));
        }

        [Fact]
        public async Task CanFindAll()
        {
            using var conn = NewInMemory();
            var repo = new SqlRepository<TestUserModel, int>(conn, new SqliteDialect());
            await repo.EnsureSchemaAsync();

            await repo.SaveAsync(new TestUserModel { UserName = "A", EmailAddress = "a@x.com" });
            await repo.SaveAsync(new TestUserModel { UserName = "B", EmailAddress = "b@x.com" });

            var all = await repo.FindAllAsync();
            Assert.Equal(2, all.Count);
        }
    }
}
