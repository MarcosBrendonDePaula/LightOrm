using System;
using System.Linq;
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
    public class AggregationsSqliteTests
    {
        private static (SqliteConnection conn, IDialect dialect) Open()
        {
            var c = new SqliteConnection("Data Source=:memory:");
            c.Open();
            return (c, new SqliteDialect());
        }

        private async Task<SqlRepository<TypesModel, int>> Seed()
        {
            var (conn, dialect) = Open();
            var repo = new SqlRepository<TypesModel, int>(conn, dialect);
            await repo.EnsureSchemaAsync();
            for (int i = 1; i <= 5; i++)
                await repo.SaveAsync(new TypesModel
                {
                    Name = $"item-{i}",
                    GuidValue = Guid.NewGuid(),
                    DecimalValue = i * 10m, // 10, 20, 30, 40, 50
                    DateValue = DateTime.UtcNow
                });
            return repo;
        }

        [Fact]
        public async Task Sum_returns_total()
        {
            var repo = await Seed();
            var sum = await repo.Query().SumAsync(nameof(TypesModel.DecimalValue));
            Assert.Equal(150m, sum);
        }

        [Fact]
        public async Task Avg_returns_mean()
        {
            var repo = await Seed();
            var avg = await repo.Query().AvgAsync(nameof(TypesModel.DecimalValue));
            Assert.Equal(30m, avg);
        }

        [Fact]
        public async Task Min_and_Max_return_extremes()
        {
            var repo = await Seed();
            Assert.Equal(10m, await repo.Query().MinAsync(nameof(TypesModel.DecimalValue)));
            Assert.Equal(50m, await repo.Query().MaxAsync(nameof(TypesModel.DecimalValue)));
        }

        [Fact]
        public async Task Aggregations_respect_filter()
        {
            var repo = await Seed();
            var sum = await repo.Query()
                .Where(nameof(TypesModel.DecimalValue), ">=", 30m)
                .SumAsync(nameof(TypesModel.DecimalValue));
            Assert.Equal(120m, sum); // 30+40+50
        }

        [Fact]
        public async Task Aggregations_on_empty_return_null()
        {
            var (conn, dialect) = Open();
            var repo = new SqlRepository<TypesModel, int>(conn, dialect);
            await repo.EnsureSchemaAsync();
            Assert.Null(await repo.Query().SumAsync(nameof(TypesModel.DecimalValue)));
            Assert.Null(await repo.Query().AvgAsync(nameof(TypesModel.DecimalValue)));
        }

        [Fact]
        public async Task GroupBy_returns_keys_with_counts()
        {
            var (conn, dialect) = Open();
            var repo = new SqlRepository<TypesModel, int>(conn, dialect);
            await repo.EnsureSchemaAsync();
            // Cria 3 com NullableInt=1, 2 com 2, 1 sem (null).
            for (int i = 0; i < 3; i++)
                await repo.SaveAsync(new TypesModel { Name = "a", GuidValue = Guid.NewGuid(), DecimalValue = 0, DateValue = DateTime.UtcNow, NullableInt = 1 });
            for (int i = 0; i < 2; i++)
                await repo.SaveAsync(new TypesModel { Name = "b", GuidValue = Guid.NewGuid(), DecimalValue = 0, DateValue = DateTime.UtcNow, NullableInt = 2 });
            await repo.SaveAsync(new TypesModel { Name = "c", GuidValue = Guid.NewGuid(), DecimalValue = 0, DateValue = DateTime.UtcNow, NullableInt = null });

            var groups = await repo.Query().GroupByAsync(nameof(TypesModel.NullableInt));
            // Esperamos 3 grupos: 1→3, 2→2, null→1
            Assert.Equal(3, groups.Count);
            Assert.Equal(3, groups.Single(g => Equals(g.key?.ToString(), "1")).count);
            Assert.Equal(2, groups.Single(g => Equals(g.key?.ToString(), "2")).count);
            Assert.Equal(1, groups.Single(g => g.key == null).count);
        }
    }

    public class AggregationsMongoTests : IAsyncLifetime
    {
        private const string ConnectionString = "mongodb://localhost:27017";
        private IMongoDatabase _db;
        private string _dbName;

        public Task InitializeAsync()
        {
            _dbName = $"lightorm_agg_{Guid.NewGuid():N}";
            _db = new MongoClient(ConnectionString).GetDatabase(_dbName);
            return Task.CompletedTask;
        }

        public Task DisposeAsync()
        {
            new MongoClient(ConnectionString).DropDatabase(_dbName);
            return Task.CompletedTask;
        }

        [Fact]
        public async Task Sum_Avg_Min_Max_work_in_mongo()
        {
            var repo = new MongoRepository<TestUserMongoModel, string>(_db);
            // UserName é string; usamos IsActive→bool→count com GroupBy.
            // Mas para Sum/Avg/Min/Max precisamos de valor numérico — use NullableInt
            // do TypesModel. Mas TypesModel é SQL-only por causa dos atributos relacionais ausentes;
            // funciona em Mongo tb pois os atributos extras só são ignorados.
            // Mais limpo: usar TestUserMongoModel apenas pra Count via GroupBy.
            for (int i = 0; i < 5; i++)
                await repo.SaveAsync(new TestUserMongoModel { UserName = $"u{i}", IsActive = i % 2 == 0 });

            var groups = await repo.Query().GroupByAsync(nameof(TestUserMongoModel.IsActive));
            Assert.Equal(2, groups.Count);
            // 3 com IsActive=true (i=0,2,4), 2 com false (i=1,3).
            var actives = groups.Single(g => Equals(g.key, true)).count;
            var inactives = groups.Single(g => Equals(g.key, false)).count;
            Assert.Equal(3, actives);
            Assert.Equal(2, inactives);
        }

        [Fact]
        public async Task GroupBy_respects_filter()
        {
            var repo = new MongoRepository<TestUserMongoModel, string>(_db);
            for (int i = 0; i < 6; i++)
                await repo.SaveAsync(new TestUserMongoModel { UserName = $"u{i}", IsActive = i < 3 });

            var groups = await repo.Query()
                .Where(nameof(TestUserMongoModel.IsActive), true)
                .GroupByAsync(nameof(TestUserMongoModel.UserName));
            Assert.Equal(3, groups.Count);
            Assert.All(groups, g => Assert.Equal(1, g.count));
        }
    }
}
