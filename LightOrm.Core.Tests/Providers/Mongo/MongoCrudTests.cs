using System;
using System.Threading.Tasks;
using LightOrm.Core.Tests.Models;
using LightOrm.Mongo;
using MongoDB.Driver;
using Xunit;

namespace LightOrm.Core.Tests
{
    public class MongoCrudTests : IAsyncLifetime
    {
        private const string ConnectionString = "mongodb://localhost:27017";
        private IMongoDatabase _db;
        private string _dbName;

        public Task InitializeAsync()
        {
            _dbName = $"lightorm_test_{Guid.NewGuid():N}";
            _db = new MongoClient(ConnectionString).GetDatabase(_dbName);
            return Task.CompletedTask;
        }

        public Task DisposeAsync()
        {
            new MongoClient(ConnectionString).DropDatabase(_dbName);
            return Task.CompletedTask;
        }

        [Fact]
        public async Task CanInsertAndFindById()
        {
            var repo = new MongoRepository<TestUserMongoModel, string>(_db);
            await repo.EnsureSchemaAsync();

            var user = new TestUserMongoModel
            {
                UserName = "Mongo User",
                EmailAddress = "m@x.com",
                IsActive = true
            };
            await repo.SaveAsync(user);

            Assert.False(string.IsNullOrEmpty(user.Id));
            var loaded = await repo.FindByIdAsync(user.Id);
            Assert.NotNull(loaded);
            Assert.Equal("Mongo User", loaded.UserName);
            Assert.True(loaded.IsActive);
        }

        [Fact]
        public async Task CanUpdateAndDelete()
        {
            var repo = new MongoRepository<TestUserMongoModel, string>(_db);
            var user = new TestUserMongoModel { UserName = "Old", EmailAddress = "o@x.com" };
            await repo.SaveAsync(user);

            user.UserName = "New";
            await repo.SaveAsync(user);

            var loaded = await repo.FindByIdAsync(user.Id);
            Assert.Equal("New", loaded.UserName);

            await repo.DeleteAsync(loaded);
            Assert.Null(await repo.FindByIdAsync(user.Id));
        }

        [Fact]
        public async Task CanFindAll()
        {
            var repo = new MongoRepository<TestUserMongoModel, string>(_db);
            await repo.SaveAsync(new TestUserMongoModel { UserName = "A", EmailAddress = "a@x.com" });
            await repo.SaveAsync(new TestUserMongoModel { UserName = "B", EmailAddress = "b@x.com" });

            var all = await repo.FindAllAsync();
            Assert.Equal(2, all.Count);
        }
    }
}
