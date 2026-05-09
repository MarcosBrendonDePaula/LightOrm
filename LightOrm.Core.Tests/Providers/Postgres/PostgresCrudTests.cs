using System;
using System.Threading.Tasks;
using LightOrm.Core.Sql;
using LightOrm.Core.Tests.Models;
using LightOrm.Postgres;
using Npgsql;
using Xunit;

namespace LightOrm.Core.Tests
{
    public class PostgresCrudTests : IAsyncLifetime
    {
        private const string AdminConnString = "Host=localhost;Port=5433;Username=postgres;Password=my-secret-pw;Database=postgres";
        private string _dbName;
        private string _testConnString;
        protected NpgsqlConnection Connection { get; private set; }

        public async Task InitializeAsync()
        {
            _dbName = $"testdb_{Guid.NewGuid():N}";
            _testConnString = $"Host=localhost;Port=5433;Username=postgres;Password=my-secret-pw;Database={_dbName}";

            using (var admin = new NpgsqlConnection(AdminConnString))
            {
                await admin.OpenAsync();
                using var cmd = new NpgsqlCommand($"CREATE DATABASE \"{_dbName}\"", admin);
                await cmd.ExecuteNonQueryAsync();
            }

            Connection = new NpgsqlConnection(_testConnString);
            await Connection.OpenAsync();
        }

        public async Task DisposeAsync()
        {
            if (Connection?.State == System.Data.ConnectionState.Open)
                await Connection.CloseAsync();
            Connection?.Dispose();

            using var admin = new NpgsqlConnection(AdminConnString);
            await admin.OpenAsync();
            using var terminate = new NpgsqlCommand(
                $"SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = '{_dbName}'", admin);
            await terminate.ExecuteNonQueryAsync();
            using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_dbName}\"", admin);
            await drop.ExecuteNonQueryAsync();
        }

        private SqlRepository<TestUserModel, int> Repo() =>
            new SqlRepository<TestUserModel, int>(Connection, new PostgresDialect());

        [Fact]
        public async Task CanCreateTableAndInsertData()
        {
            var repo = Repo();
            await repo.EnsureSchemaAsync();

            var user = new TestUserModel
            {
                UserName = "Postgres User",
                EmailAddress = "p@x.com",
                IsActive = true
            };
            await repo.SaveAsync(user);

            Assert.True(user.Id > 0);
            var loaded = await repo.FindByIdAsync(user.Id);
            Assert.NotNull(loaded);
            Assert.Equal("Postgres User", loaded.UserName);
            Assert.True(loaded.IsActive);
        }

        [Fact]
        public async Task CanUpdateAndDelete()
        {
            var repo = Repo();
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
            var repo = Repo();
            await repo.EnsureSchemaAsync();

            await repo.SaveAsync(new TestUserModel { UserName = "A", EmailAddress = "a@x.com" });
            await repo.SaveAsync(new TestUserModel { UserName = "B", EmailAddress = "b@x.com" });

            var all = await repo.FindAllAsync();
            Assert.Equal(2, all.Count);
        }
    }

    public class PostgresRelationshipTests : PostgresCrudTests
    {
        [Fact]
        public async Task OneToOne_OneToMany_ManyToMany_load_correctly()
        {
            var dialect = new PostgresDialect();
            var addresses = new SqlRepository<AddressModel, int>(Connection, dialect);
            var students = new SqlRepository<StudentModel, int>(Connection, dialect);
            var assignments = new SqlRepository<AssignmentModel, int>(Connection, dialect);
            var courses = new SqlRepository<CourseModel, int>(Connection, dialect);
            var links = new SqlRepository<StudentCourseLink, int>(Connection, dialect);
            await addresses.EnsureSchemaAsync();
            await courses.EnsureSchemaAsync();
            await students.EnsureSchemaAsync();
            await assignments.EnsureSchemaAsync();
            await links.EnsureSchemaAsync();

            var addr = await addresses.SaveAsync(new AddressModel { Street = "R1", City = "SP" });
            var stu = await students.SaveAsync(new StudentModel { Name = "Ana", AddressId = addr.Id });
            await assignments.SaveAsync(new AssignmentModel { Title = "T1", StudentId = stu.Id });
            await assignments.SaveAsync(new AssignmentModel { Title = "T2", StudentId = stu.Id });
            var c1 = await courses.SaveAsync(new CourseModel { Name = "Math" });
            await links.SaveAsync(new StudentCourseLink { StudentId = stu.Id, CourseId = c1.Id });

            var loaded = await students.FindByIdAsync(stu.Id, includeRelated: true);
            Assert.NotNull(loaded.Address);
            Assert.Equal("R1", loaded.Address.Street);
            Assert.Equal(2, loaded.Assignments.Length);
            Assert.Single(loaded.Courses);
        }
    }
}
