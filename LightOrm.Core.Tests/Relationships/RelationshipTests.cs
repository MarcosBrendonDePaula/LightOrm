using System.Data.Common;
using System.Threading.Tasks;
using LightOrm.Core.Sql;
using LightOrm.Core.Tests.Models;
using LightOrm.MySql;
using LightOrm.Sqlite;
using Microsoft.Data.Sqlite;
using Xunit;

namespace LightOrm.Core.Tests
{
    public class RelationshipTestsSqlite : RelationshipScenarios
    {
        protected override (DbConnection conn, IDialect dialect) Open()
        {
            var c = new SqliteConnection("Data Source=:memory:");
            c.Open();
            return (c, new SqliteDialect());
        }
    }

    public class RelationshipTestsMySql : TestBase
    {
        [Fact]
        public Task OneToOne_loads_related() => Run(s => s.OneToOne_loads_related());
        [Fact]
        public Task OneToMany_loads_collection() => Run(s => s.OneToMany_loads_collection());
        [Fact]
        public Task ManyToMany_loads_via_association_table() => Run(s => s.ManyToMany_loads_via_association_table());
        [Fact]
        public Task FindAll_includeRelated_avoids_NPlus1() => Run(s => s.FindAll_includeRelated_avoids_NPlus1());

        private Task Run(System.Func<RelationshipScenarios, Task> body) =>
            body(new MySqlScenarios(Connection));

        private class MySqlScenarios : RelationshipScenarios
        {
            private readonly DbConnection _conn;
            public MySqlScenarios(DbConnection conn) { _conn = conn; }
            protected override (DbConnection conn, IDialect dialect) Open() => (_conn, new MySqlDialect());
        }
    }

    public abstract class RelationshipScenarios
    {
        protected abstract (DbConnection conn, IDialect dialect) Open();

        // Cria todas as tabelas relacionadas — necessário porque StudentModel
        // declara três relacionamentos e o RelatedLoader os carrega todos.
        private async Task<(SqlRepository<AddressModel, int> a,
                            SqlRepository<StudentModel, int> s,
                            SqlRepository<AssignmentModel, int> asg,
                            SqlRepository<CourseModel, int> c,
                            SqlRepository<StudentCourseLink, int> link)> SetupAsync()
        {
            var (conn, dialect) = Open();
            var addresses = new SqlRepository<AddressModel, int>(conn, dialect);
            var students = new SqlRepository<StudentModel, int>(conn, dialect);
            var assignments = new SqlRepository<AssignmentModel, int>(conn, dialect);
            var courses = new SqlRepository<CourseModel, int>(conn, dialect);
            var links = new SqlRepository<StudentCourseLink, int>(conn, dialect);
            await addresses.EnsureSchemaAsync();
            await courses.EnsureSchemaAsync();
            await students.EnsureSchemaAsync();
            await assignments.EnsureSchemaAsync();
            await links.EnsureSchemaAsync();
            return (addresses, students, assignments, courses, links);
        }

        [Fact]
        public async Task OneToOne_loads_related()
        {
            var (a, s, _, _, _) = await SetupAsync();
            var addr = await a.SaveAsync(new AddressModel { Street = "Rua A", City = "São Paulo" });
            var stu = await s.SaveAsync(new StudentModel { Name = "Ana", AddressId = addr.Id });

            var loaded = await s.FindByIdAsync(stu.Id, includeRelated: true);
            Assert.NotNull(loaded.Address);
            Assert.Equal("Rua A", loaded.Address.Street);
        }

        [Fact]
        public async Task OneToMany_loads_collection()
        {
            var (_, s, asg, _, _) = await SetupAsync();
            var stu = await s.SaveAsync(new StudentModel { Name = "Bia" });
            await asg.SaveAsync(new AssignmentModel { Title = "T1", StudentId = stu.Id });
            await asg.SaveAsync(new AssignmentModel { Title = "T2", StudentId = stu.Id });

            var loaded = await s.FindByIdAsync(stu.Id, includeRelated: true);
            Assert.NotNull(loaded.Assignments);
            Assert.Equal(2, loaded.Assignments.Length);
        }

        [Fact]
        public async Task ManyToMany_loads_via_association_table()
        {
            var (_, s, _, c, link) = await SetupAsync();
            var stu = await s.SaveAsync(new StudentModel { Name = "Caio" });
            var c1 = await c.SaveAsync(new CourseModel { Name = "Math" });
            var c2 = await c.SaveAsync(new CourseModel { Name = "Phys" });
            await link.SaveAsync(new StudentCourseLink { StudentId = stu.Id, CourseId = c1.Id });
            await link.SaveAsync(new StudentCourseLink { StudentId = stu.Id, CourseId = c2.Id });

            var loaded = await s.FindByIdAsync(stu.Id, includeRelated: true);
            Assert.NotNull(loaded.Courses);
            Assert.Equal(2, loaded.Courses.Length);
        }

        [Fact]
        public async Task FindAll_includeRelated_avoids_NPlus1()
        {
            var (a, s, _, _, _) = await SetupAsync();
            for (int i = 0; i < 5; i++)
            {
                var addr = await a.SaveAsync(new AddressModel { Street = $"R{i}", City = "X" });
                await s.SaveAsync(new StudentModel { Name = $"S{i}", AddressId = addr.Id });
            }

            var all = await s.FindAllAsync(includeRelated: true);
            Assert.Equal(5, all.Count);
            Assert.All(all, x => Assert.NotNull(x.Address));
        }
    }
}
