using System;
using System.Threading.Tasks;
using LightOrm.Core.Tests.Models;
using LightOrm.Mongo;
using MongoDB.Driver;
using Xunit;

namespace LightOrm.Core.Tests
{
    public class MongoEmbeddedTests : IAsyncLifetime
    {
        private const string ConnectionString = "mongodb://localhost:27017";
        private IMongoDatabase _db;
        private string _dbName;

        public Task InitializeAsync()
        {
            _dbName = $"lightorm_embed_{Guid.NewGuid():N}";
            _db = new MongoClient(ConnectionString).GetDatabase(_dbName);
            return Task.CompletedTask;
        }

        public Task DisposeAsync()
        {
            new MongoClient(ConnectionString).DropDatabase(_dbName);
            return Task.CompletedTask;
        }

        private MongoRepository<TestUserWithEmbedsModel, string> Repo() =>
            new MongoRepository<TestUserWithEmbedsModel, string>(_db);

        [Fact]
        public async Task Can_save_and_load_array_of_embeds()
        {
            var repo = Repo();
            var entity = new TestUserWithEmbedsModel
            {
                Name = "Ana",
                Addresses = new[]
                {
                    new EmbeddedAddress { Street = "R1", City = "SP", Zip = "01000" },
                    new EmbeddedAddress { Street = "R2", City = "RJ", Zip = "20000" }
                }
            };
            await repo.SaveAsync(entity);

            var loaded = await repo.FindByIdAsync(entity.Id);
            Assert.NotNull(loaded);
            Assert.Equal(2, loaded.Addresses.Length);
            Assert.Equal("R1", loaded.Addresses[0].Street);
            Assert.Equal("RJ", loaded.Addresses[1].City);
        }

        [Fact]
        public async Task Can_save_and_load_single_embed()
        {
            var repo = Repo();
            var entity = new TestUserWithEmbedsModel
            {
                Name = "Bia",
                PrimaryAddress = new EmbeddedAddress { Street = "Av Principal", City = "BH", Zip = "30000" }
            };
            await repo.SaveAsync(entity);

            var loaded = await repo.FindByIdAsync(entity.Id);
            Assert.NotNull(loaded.PrimaryAddress);
            Assert.Equal("Av Principal", loaded.PrimaryAddress.Street);
            Assert.Equal("BH", loaded.PrimaryAddress.City);
        }

        [Fact]
        public async Task Null_and_empty_embeds_roundtrip_safely()
        {
            var repo = Repo();
            var entity = new TestUserWithEmbedsModel
            {
                Name = "Caio",
                Addresses = Array.Empty<EmbeddedAddress>(),
                PrimaryAddress = null
            };
            await repo.SaveAsync(entity);

            var loaded = await repo.FindByIdAsync(entity.Id);
            Assert.NotNull(loaded.Addresses);
            Assert.Empty(loaded.Addresses);
            Assert.Null(loaded.PrimaryAddress);
        }

        [Fact]
        public async Task Update_replaces_embeds_completely()
        {
            var repo = Repo();
            var entity = new TestUserWithEmbedsModel
            {
                Name = "Dani",
                Addresses = new[]
                {
                    new EmbeddedAddress { Street = "antiga 1", City = "SP" },
                    new EmbeddedAddress { Street = "antiga 2", City = "SP" }
                }
            };
            await repo.SaveAsync(entity);

            entity.Addresses = new[]
            {
                new EmbeddedAddress { Street = "nova", City = "RJ" }
            };
            await repo.SaveAsync(entity);

            var loaded = await repo.FindByIdAsync(entity.Id);
            Assert.Single(loaded.Addresses);
            Assert.Equal("nova", loaded.Addresses[0].Street);
        }

        [Fact]
        public async Task FindAll_loads_embeds_for_each_root()
        {
            var repo = Repo();
            for (int i = 0; i < 3; i++)
            {
                await repo.SaveAsync(new TestUserWithEmbedsModel
                {
                    Name = $"u{i}",
                    Addresses = new[]
                    {
                        new EmbeddedAddress { Street = $"R{i}-1", City = "X" },
                        new EmbeddedAddress { Street = $"R{i}-2", City = "X" }
                    }
                });
            }

            var all = await repo.FindAllAsync();
            Assert.Equal(3, all.Count);
            Assert.All(all, u =>
            {
                Assert.NotNull(u.Addresses);
                Assert.Equal(2, u.Addresses.Length);
            });
        }
    }
}
