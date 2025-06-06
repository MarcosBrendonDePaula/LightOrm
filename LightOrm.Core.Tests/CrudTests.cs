using Xunit;
using System.Threading.Tasks;
using LightOrm.Core.Models;
using LightOrm.Core.Tests.Models;
using MySql.Data.MySqlClient;

namespace LightOrm.Core.Tests
{
    public class CrudTests : TestBase
    {
        [Fact]
        public async Task CanCreateTableAndInsertData()
        {
            // Arrange
            var userModel = new TestUserModel();
            await userModel.EnsureTableExistsAsync(Connection);

            var user = new TestUserModel
            {
                UserName = "Test User",
                EmailAddress = "test@example.com",
                IsActive = true
            };

            // Act
            await user.SaveAsync(Connection);

            // Assert
            Assert.True(user.Id > 0);

            var loadedUser = await TestUserModel.FindByIdAsync(Connection, user.Id);
            Assert.NotNull(loadedUser);
            Assert.Equal("Test User", loadedUser.UserName);
            Assert.Equal("test@example.com", loadedUser.EmailAddress);
            Assert.True(loadedUser.IsActive);
        }

        [Fact]
        public async Task CanUpdateData()
        {
            // Arrange
            var userModel = new TestUserModel();
            await userModel.EnsureTableExistsAsync(Connection);

            var user = new TestUserModel
            {
                UserName = "User to Update",
                EmailAddress = "update@example.com",
                IsActive = false
            };
            await user.SaveAsync(Connection);

            // Act
            user.UserName = "Updated User";
            user.IsActive = true;
            await user.SaveAsync(Connection);

            // Assert
            var loadedUser = await TestUserModel.FindByIdAsync(Connection, user.Id);
            Assert.NotNull(loadedUser);
            Assert.Equal("Updated User", loadedUser.UserName);
            Assert.True(loadedUser.IsActive);
        }

        [Fact]
        public async Task CanDeleteData()
        {
            // Arrange
            var userModel = new TestUserModel();
            await userModel.EnsureTableExistsAsync(Connection);

            var user = new TestUserModel
            {
                UserName = "User to Delete",
                EmailAddress = "delete@example.com",
                IsActive = true
            };
            await user.SaveAsync(Connection);

            // Act
            await user.DeleteAsync(Connection);

            // Assert
            var loadedUser = await TestUserModel.FindByIdAsync(Connection, user.Id);
            Assert.Null(loadedUser);
        }

        [Fact]
        public async Task CanFindAllData()
        {
            // Arrange
            var userModel = new TestUserModel();
            await userModel.EnsureTableExistsAsync(Connection);

            await new TestUserModel { UserName = "User1", EmailAddress = "user1@example.com" }.SaveAsync(Connection);
            await new TestUserModel { UserName = "User2", EmailAddress = "user2@example.com" }.SaveAsync(Connection);

            // Act
            var allUsers = await TestUserModel.FindAllAsync(Connection);

            // Assert
            Assert.NotNull(allUsers);
            Assert.Equal(2, allUsers.Count);
        }
    }
}

