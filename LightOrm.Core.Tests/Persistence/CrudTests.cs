using System.Linq;
using System.Threading.Tasks;
using LightOrm.Core.Sql;
using LightOrm.Core.Tests.Models;
using LightOrm.MySql;
using Xunit;

namespace LightOrm.Core.Tests
{
    public class CrudTests : TestBase
    {
        private SqlRepository<TestUserModel, int> Repo() =>
            new SqlRepository<TestUserModel, int>(Connection, new MySqlDialect());

        [Fact]
        public async Task CanCreateTableAndInsertData()
        {
            var repo = Repo();
            await repo.EnsureSchemaAsync();

            var user = new TestUserModel
            {
                UserName = "Test User",
                EmailAddress = "test@example.com",
                IsActive = true
            };
            await repo.SaveAsync(user);

            Assert.True(user.Id > 0);

            var loaded = await repo.FindByIdAsync(user.Id);
            Assert.NotNull(loaded);
            Assert.Equal("Test User", loaded.UserName);
            Assert.Equal("test@example.com", loaded.EmailAddress);
            Assert.True(loaded.IsActive);
        }

        [Fact]
        public async Task CanUpdateData()
        {
            var repo = Repo();
            await repo.EnsureSchemaAsync();

            var user = new TestUserModel { UserName = "User to Update", EmailAddress = "u@x.com", IsActive = false };
            await repo.SaveAsync(user);

            user.UserName = "Updated User";
            user.IsActive = true;
            await repo.SaveAsync(user);

            var loaded = await repo.FindByIdAsync(user.Id);
            Assert.Equal("Updated User", loaded.UserName);
            Assert.True(loaded.IsActive);
        }

        [Fact]
        public async Task CanDeleteData()
        {
            var repo = Repo();
            await repo.EnsureSchemaAsync();

            var user = new TestUserModel { UserName = "User to Delete", EmailAddress = "d@x.com", IsActive = true };
            await repo.SaveAsync(user);
            await repo.DeleteAsync(user);

            Assert.Null(await repo.FindByIdAsync(user.Id));
        }

        [Fact]
        public async Task CanFindAllData()
        {
            var repo = Repo();
            await repo.EnsureSchemaAsync();

            await repo.SaveAsync(new TestUserModel { UserName = "User1", EmailAddress = "u1@x.com" });
            await repo.SaveAsync(new TestUserModel { UserName = "User2", EmailAddress = "u2@x.com" });

            var all = await repo.FindAllAsync();
            Assert.Equal(2, all.Count);
            Assert.Contains(all, u => u.UserName == "User1");
            Assert.Contains(all, u => u.UserName == "User2");
        }
    }
}
