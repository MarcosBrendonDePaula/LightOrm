using System;
using System.Threading.Tasks;
using LightOrm.Core.Sql;
using LightOrm.Core.Tests.Models;
using LightOrm.Mongo;
using MongoDB.Driver;
using Xunit;

namespace LightOrm.Core.Tests
{
    public class MongoSoftDeleteTests : IAsyncLifetime
    {
        private const string ConnectionString = "mongodb://localhost:27017";
        private IMongoDatabase _db;
        private string _dbName;

        public Task InitializeAsync()
        {
            _dbName = $"lightorm_mongo_sd_{Guid.NewGuid():N}";
            _db = new MongoClient(ConnectionString).GetDatabase(_dbName);
            return Task.CompletedTask;
        }

        public Task DisposeAsync()
        {
            new MongoClient(ConnectionString).DropDatabase(_dbName);
            return Task.CompletedTask;
        }

        [Fact]
        public async Task Delete_marks_deletedAt_and_keeps_document()
        {
            var repo = new MongoRepository<SoftDeletedMongoModel, string>(_db);
            var entity = new SoftDeletedMongoModel { Name = "x" };
            await repo.SaveAsync(entity);
            await repo.DeleteAsync(entity);

            Assert.Null(await repo.FindByIdAsync(entity.Id));
            var raw = await repo.FindByIdIncludingDeletedAsync(entity.Id);
            Assert.NotNull(raw);
            Assert.NotNull(raw.DeletedAt);
        }

        [Fact]
        public async Task FindAll_default_filters_deleted()
        {
            var repo = new MongoRepository<SoftDeletedMongoModel, string>(_db);
            var a = new SoftDeletedMongoModel { Name = "a" };
            var b = new SoftDeletedMongoModel { Name = "b" };
            await repo.SaveAsync(a);
            await repo.SaveAsync(b);
            await repo.DeleteAsync(b);

            var visible = await repo.FindAllAsync();
            Assert.Single(visible);
            Assert.Equal("a", visible[0].Name);

            var all = await repo.FindAllIncludingDeletedAsync();
            Assert.Equal(2, all.Count);
        }

        [Fact]
        public async Task Restore_unsets_deletedAt()
        {
            var repo = new MongoRepository<SoftDeletedMongoModel, string>(_db);
            var entity = new SoftDeletedMongoModel { Name = "x" };
            await repo.SaveAsync(entity);
            await repo.DeleteAsync(entity);
            Assert.NotNull(entity.DeletedAt);

            await repo.RestoreAsync(entity);
            Assert.Null(entity.DeletedAt);

            var loaded = await repo.FindByIdAsync(entity.Id);
            Assert.NotNull(loaded);
            Assert.Null(loaded.DeletedAt);
        }
    }

    public class MongoVersionTests : IAsyncLifetime
    {
        private const string ConnectionString = "mongodb://localhost:27017";
        private IMongoDatabase _db;
        private string _dbName;

        public Task InitializeAsync()
        {
            _dbName = $"lightorm_mongo_ver_{Guid.NewGuid():N}";
            _db = new MongoClient(ConnectionString).GetDatabase(_dbName);
            return Task.CompletedTask;
        }

        public Task DisposeAsync()
        {
            new MongoClient(ConnectionString).DropDatabase(_dbName);
            return Task.CompletedTask;
        }

        [Fact]
        public async Task Insert_initializes_version_to_one()
        {
            var repo = new MongoRepository<VersionedMongoModel, string>(_db);
            var v = new VersionedMongoModel { Name = "first" };
            await repo.SaveAsync(v);
            Assert.Equal(1, v.RowVersion);

            var loaded = await repo.FindByIdAsync(v.Id);
            Assert.Equal(1, loaded.RowVersion);
        }

        [Fact]
        public async Task Update_increments_version()
        {
            var repo = new MongoRepository<VersionedMongoModel, string>(_db);
            var v = new VersionedMongoModel { Name = "v1" };
            await repo.SaveAsync(v);

            v.Name = "v2";
            await repo.SaveAsync(v);
            Assert.Equal(2, v.RowVersion);

            v.Name = "v3";
            await repo.SaveAsync(v);
            Assert.Equal(3, v.RowVersion);
        }

        [Fact]
        public async Task Concurrent_update_throws_DbConcurrencyException()
        {
            var repo = new MongoRepository<VersionedMongoModel, string>(_db);
            await repo.SaveAsync(new VersionedMongoModel { Name = "shared" });

            // Pega via FindAll para garantir hidratação completa do RowVersion.
            var seed = (await repo.FindAllAsync())[0];
            var a = await repo.FindByIdAsync(seed.Id);
            var b = await repo.FindByIdAsync(seed.Id);
            Assert.Equal(1, a.RowVersion);
            Assert.Equal(1, b.RowVersion);

            a.Name = "by-a";
            await repo.SaveAsync(a);
            Assert.Equal(2, a.RowVersion);

            b.Name = "by-b";
            await Assert.ThrowsAsync<DbConcurrencyException>(() => repo.SaveAsync(b));

            var current = await repo.FindByIdAsync(seed.Id);
            Assert.Equal("by-a", current.Name);
            Assert.Equal(2, current.RowVersion);
        }
    }
}
