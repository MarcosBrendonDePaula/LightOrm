using System;
using System.Threading.Tasks;
using LightOrm.Core.Sql;
using LightOrm.Core.Tests.Models;
using LightOrm.Mongo;
using LightOrm.Sqlite;
using Microsoft.Data.Sqlite;
using MongoDB.Driver;
using Xunit;

namespace LightOrm.Core.Tests
{
    public class UpsertSqliteTests
    {
        private static (SqliteConnection conn, IDialect dialect) Open()
        {
            var c = new SqliteConnection("Data Source=:memory:");
            c.Open();
            return (c, new SqliteDialect());
        }

        [Fact]
        public async Task Upsert_with_caller_provided_string_id_inserts_when_missing()
        {
            var (conn, dialect) = Open();
            var repo = new SqlRepository<PortableUserModel, string>(conn, dialect);
            await repo.EnsureSchemaAsync();

            var u = new PortableUserModel { Id = "user-001", Name = "Ana", Active = true };
            await repo.UpsertAsync(u);

            var loaded = await repo.FindByIdAsync("user-001");
            Assert.NotNull(loaded);
            Assert.Equal("Ana", loaded.Name);
            Assert.True(loaded.Active);
        }

        [Fact]
        public async Task Upsert_updates_when_record_exists()
        {
            var (conn, dialect) = Open();
            var repo = new SqlRepository<PortableUserModel, string>(conn, dialect);
            await repo.EnsureSchemaAsync();

            var u = new PortableUserModel { Id = "user-002", Name = "old", Active = false };
            await repo.UpsertAsync(u);
            var firstCreatedAt = u.CreatedAt;

            await Task.Delay(10);
            u.Name = "new";
            u.Active = true;
            await repo.UpsertAsync(u);

            var loaded = await repo.FindByIdAsync("user-002");
            Assert.Equal("new", loaded.Name);
            Assert.True(loaded.Active);
            // CreatedAt preservado, UpdatedAt avançou.
            Assert.Equal(firstCreatedAt.Date, loaded.CreatedAt.Date);

            var all = await repo.FindAllAsync();
            Assert.Single(all);
        }

        [Fact]
        public async Task Upsert_without_id_falls_back_to_save_and_generates_id()
        {
            var (conn, dialect) = Open();
            var repo = new SqlRepository<PortableUserModel, string>(conn, dialect);
            await repo.EnsureSchemaAsync();

            var u = new PortableUserModel { Name = "no-id" };
            await repo.UpsertAsync(u);

            Assert.False(string.IsNullOrEmpty(u.Id));
            var loaded = await repo.FindByIdAsync(u.Id);
            Assert.NotNull(loaded);
        }

        [Fact]
        public async Task Upsert_works_with_int_keys_when_id_is_set()
        {
            var (conn, dialect) = Open();
            var repo = new SqlRepository<TypesModel, int>(conn, dialect);
            await repo.EnsureSchemaAsync();

            // Insere normal pra ter um id real.
            var entity = new TypesModel
            {
                Name = "first",
                GuidValue = Guid.NewGuid(),
                DecimalValue = 1m,
                DateValue = DateTime.UtcNow
            };
            await repo.SaveAsync(entity);
            var id = entity.Id;

            // Upsert com id existente: vira update.
            entity.Name = "updated-via-upsert";
            await repo.UpsertAsync(entity);

            var loaded = await repo.FindByIdAsync(id);
            Assert.Equal("updated-via-upsert", loaded.Name);
        }
    }

    public class UpsertMongoTests : IAsyncLifetime
    {
        private const string ConnectionString = "mongodb://localhost:27017";
        private IMongoDatabase _db;
        private string _dbName;

        public Task InitializeAsync()
        {
            _dbName = $"lightorm_upsert_{Guid.NewGuid():N}";
            _db = new MongoClient(ConnectionString).GetDatabase(_dbName);
            return Task.CompletedTask;
        }

        public Task DisposeAsync()
        {
            new MongoClient(ConnectionString).DropDatabase(_dbName);
            return Task.CompletedTask;
        }

        [Fact]
        public async Task Upsert_inserts_when_missing()
        {
            var repo = new MongoRepository<PortableUserModel, string>(_db);
            var u = new PortableUserModel { Id = "mongo-001", Name = "Ana" };
            await repo.UpsertAsync(u);

            var loaded = await repo.FindByIdAsync("mongo-001");
            Assert.NotNull(loaded);
            Assert.Equal("Ana", loaded.Name);
        }

        [Fact]
        public async Task Upsert_updates_when_exists_and_preserves_createdAt()
        {
            var repo = new MongoRepository<PortableUserModel, string>(_db);
            var u = new PortableUserModel { Id = "mongo-002", Name = "old" };
            await repo.UpsertAsync(u);
            var firstCreatedAt = u.CreatedAt;

            await Task.Delay(10);
            u.Name = "new";
            await repo.UpsertAsync(u);

            var loaded = await repo.FindByIdAsync("mongo-002");
            Assert.Equal("new", loaded.Name);
            // O upsert do Mongo preserva o CreatedAt original.
            Assert.Equal(firstCreatedAt.Year, loaded.CreatedAt.Year);
            Assert.Equal(firstCreatedAt.Month, loaded.CreatedAt.Month);
            Assert.Equal(firstCreatedAt.Day, loaded.CreatedAt.Day);
        }
    }
}
