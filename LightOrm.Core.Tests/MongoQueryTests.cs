using System;
using System.Threading.Tasks;
using LightOrm.Core.Models;
using LightOrm.Core.Tests.Models;
using LightOrm.Mongo;
using MongoDB.Driver;
using Xunit;

namespace LightOrm.Core.Tests
{
    public class MongoQueryTests : IAsyncLifetime
    {
        private const string ConnectionString = "mongodb://localhost:27017";
        private IMongoDatabase _db;
        private string _dbName;

        public Task InitializeAsync()
        {
            _dbName = $"lightorm_query_{Guid.NewGuid():N}";
            _db = new MongoClient(ConnectionString).GetDatabase(_dbName);
            return Task.CompletedTask;
        }

        public Task DisposeAsync()
        {
            new MongoClient(ConnectionString).DropDatabase(_dbName);
            return Task.CompletedTask;
        }

        private async Task<MongoRepository<TestUserMongoModel, string>> Seed()
        {
            var repo = new MongoRepository<TestUserMongoModel, string>(_db);
            for (int i = 1; i <= 10; i++)
            {
                await repo.SaveAsync(new TestUserMongoModel
                {
                    UserName = $"user-{i:D2}",
                    EmailAddress = $"user{i}@test.com",
                    IsActive = i % 2 == 0
                });
            }
            return repo;
        }

        [Fact]
        public async Task Where_equals_works()
        {
            var repo = await Seed();
            var result = await repo.Query()
                .Where(nameof(TestUserMongoModel.UserName), "user-05")
                .ToListAsync();
            Assert.Single(result);
        }

        [Fact]
        public async Task Where_with_operators_works()
        {
            var repo = await Seed();
            var actives = await repo.Query()
                .Where(nameof(TestUserMongoModel.IsActive), true)
                .ToListAsync();
            Assert.Equal(5, actives.Count);
            Assert.All(actives, u => Assert.True(u.IsActive));
        }

        [Fact]
        public async Task WhereIn_works()
        {
            var repo = await Seed();
            var some = await repo.Query()
                .WhereIn(nameof(TestUserMongoModel.UserName), new object[] { "user-01", "user-05", "user-10" })
                .ToListAsync();
            Assert.Equal(3, some.Count);

            var none = await repo.Query()
                .WhereIn(nameof(TestUserMongoModel.UserName), Array.Empty<object>())
                .ToListAsync();
            Assert.Empty(none);
        }

        [Fact]
        public async Task Like_translates_to_regex()
        {
            var repo = await Seed();
            var result = await repo.Query()
                .Where(nameof(TestUserMongoModel.UserName), "LIKE", "user-0%")
                .ToListAsync();
            Assert.Equal(9, result.Count); // user-01..09
        }

        [Fact]
        public async Task OrderBy_take_skip_paginate()
        {
            var repo = await Seed();
            var page = await repo.Query()
                .OrderBy(nameof(TestUserMongoModel.UserName))
                .Skip(3)
                .Take(2)
                .ToListAsync();
            Assert.Equal(2, page.Count);
            Assert.Equal("user-04", page[0].UserName);
            Assert.Equal("user-05", page[1].UserName);
        }

        [Fact]
        public async Task Count_and_Any_work()
        {
            var repo = await Seed();
            var total = await repo.Query().CountAsync();
            Assert.Equal(10, total);

            var actives = await repo.Query()
                .Where(nameof(TestUserMongoModel.IsActive), true)
                .CountAsync();
            Assert.Equal(5, actives);

            Assert.True(await repo.Query().AnyAsync());
            Assert.False(await repo.Query()
                .Where(nameof(TestUserMongoModel.UserName), "inexistente")
                .AnyAsync());
        }

        [Fact]
        public async Task Invalid_property_throws_clear_error()
        {
            var repo = await Seed();
            var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
                repo.Query().Where("PropriedadeQueNaoExiste", "x").ToListAsync());
            Assert.Contains("PropriedadeQueNaoExiste", ex.Message);
        }
    }
}
