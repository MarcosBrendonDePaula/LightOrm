using System;
using System.Threading.Tasks;
using LightOrm.Core;
using LightOrm.Core.Attributes;
using LightOrm.Core.Models;
using LightOrm.Core.Sql;
using LightOrm.Mongo;
using LightOrm.Sqlite;
using Microsoft.Data.Sqlite;
using MongoDB.Driver;
using Xunit;

namespace LightOrm.Core.Tests
{
    // Modelo único compartilhado entre SQL e Mongo. TId=string viabiliza isso:
    // no Mongo vira ObjectId; no SQL vira chave VARCHAR gerada no app (Guid.NewGuid().ToString("N")).
    public class PortableUserModel : BaseModel<PortableUserModel, string>
    {
        public override string TableName => "portable_users";

        [Column("name", length: 100)]
        public string Name { get; set; }

        [Column("active")]
        public bool Active { get; set; }
    }

    public class PortableModelTests : IAsyncLifetime
    {
        private const string MongoConn = "mongodb://localhost:27017";
        private SqliteConnection _sqliteConn;
        private IMongoDatabase _mongoDb;
        private string _mongoDbName;

        public async Task InitializeAsync()
        {
            _sqliteConn = new SqliteConnection("Data Source=:memory:");
            _sqliteConn.Open();
            _mongoDbName = $"lightorm_portable_{Guid.NewGuid():N}";
            _mongoDb = new MongoClient(MongoConn).GetDatabase(_mongoDbName);
            await Task.CompletedTask;
        }

        public Task DisposeAsync()
        {
            _sqliteConn?.Dispose();
            new MongoClient(MongoConn).DropDatabase(_mongoDbName);
            return Task.CompletedTask;
        }

        [Fact]
        public async Task Same_query_works_against_sqlite_and_mongo_via_IQuery()
        {
            IRepository<PortableUserModel, string> sqlite = RepositoryFactory.Sql<PortableUserModel, string>(
                _sqliteConn, new SqliteDialect());
            IRepository<PortableUserModel, string> mongo = new MongoRepository<PortableUserModel, string>(_mongoDb);
            await sqlite.EnsureSchemaAsync();
            await mongo.EnsureSchemaAsync();

            foreach (var repo in new[] { sqlite, mongo })
            {
                for (int i = 1; i <= 5; i++)
                    await repo.SaveAsync(new PortableUserModel { Name = $"u-{i}", Active = i % 2 == 0 });

                // O mesmo código de Query roda contra os dois backends.
                var actives = await repo.Query()
                    .Where(nameof(PortableUserModel.Active), true)
                    .OrderBy(nameof(PortableUserModel.Name))
                    .ToListAsync();
                Assert.Equal(2, actives.Count);
                Assert.All(actives, u => Assert.True(u.Active));

                var count = await repo.Query().CountAsync();
                Assert.Equal(5, count);
            }
        }

        [Fact]
        public async Task Same_model_works_against_sqlite_and_mongo_via_IRepository()
        {
            // O código de negócio usa apenas IRepository — composition root escolhe.
            IRepository<PortableUserModel, string> sqlite = RepositoryFactory.Sql<PortableUserModel, string>(
                _sqliteConn, new SqliteDialect());
            IRepository<PortableUserModel, string> mongo = new MongoRepository<PortableUserModel, string>(_mongoDb);

            await sqlite.EnsureSchemaAsync();
            await mongo.EnsureSchemaAsync();

            await ExerciseRepository(sqlite, "ana-sqlite");
            await ExerciseRepository(mongo, "ana-mongo");
        }

        private static async Task ExerciseRepository(IRepository<PortableUserModel, string> repo, string label)
        {
            var entity = new PortableUserModel { Name = label, Active = true };
            await repo.SaveAsync(entity);
            Assert.False(string.IsNullOrEmpty(entity.Id), $"{label}: id deveria ser gerado");

            var loaded = await repo.FindByIdAsync(entity.Id);
            Assert.NotNull(loaded);
            Assert.Equal(label, loaded.Name);
            Assert.True(loaded.Active);

            entity.Active = false;
            await repo.SaveAsync(entity);
            var updated = await repo.FindByIdAsync(entity.Id);
            Assert.False(updated.Active);

            var all = await repo.FindAllAsync();
            Assert.Single(all);

            await repo.DeleteAsync(entity);
            Assert.Null(await repo.FindByIdAsync(entity.Id));
        }

        [Fact]
        public async Task Sql_with_string_id_generates_id_on_insert()
        {
            var repo = new SqlRepository<PortableUserModel, string>(_sqliteConn, new SqliteDialect());
            await repo.EnsureSchemaAsync();

            var u1 = new PortableUserModel { Name = "u1" };
            var u2 = new PortableUserModel { Name = "u2" };
            await repo.SaveAsync(u1);
            await repo.SaveAsync(u2);

            Assert.False(string.IsNullOrEmpty(u1.Id));
            Assert.False(string.IsNullOrEmpty(u2.Id));
            Assert.NotEqual(u1.Id, u2.Id);
        }

        // NOTA: id customizado pelo caller (ex.: u.Id = "user-001" antes do SaveAsync)
        // é interpretado como "registro existente" e segue caminho de UPDATE — que
        // não acha a linha e vira no-op. O contrato hoje é: deixe Id vazio que o
        // framework gera. Upsert explícito fica para PR futuro.

        [Fact]
        public async Task Sql_savemany_with_string_id_generates_unique_ids()
        {
            var repo = new SqlRepository<PortableUserModel, string>(_sqliteConn, new SqliteDialect());
            await repo.EnsureSchemaAsync();

            var batch = new[]
            {
                new PortableUserModel { Name = "a" },
                new PortableUserModel { Name = "b" },
                new PortableUserModel { Name = "c" }
            };
            await repo.SaveManyAsync(batch);

            var ids = new System.Collections.Generic.HashSet<string>();
            foreach (var e in batch)
            {
                Assert.False(string.IsNullOrEmpty(e.Id));
                Assert.True(ids.Add(e.Id), "Ids devem ser únicos");
            }

            var all = await repo.FindAllAsync();
            Assert.Equal(3, all.Count);
        }
    }
}
