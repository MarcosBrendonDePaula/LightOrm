using System.Linq;
using System.Threading.Tasks;
using LightOrm.Core.Sql;
using LightOrm.Core.Tests.Models;
using LightOrm.Sqlite;
using Microsoft.Data.Sqlite;
using Xunit;

namespace LightOrm.Core.Tests
{
    public class RelatedLoaderChunkingTests
    {
        private static (SqliteConnection conn, IDialect dialect) Open()
        {
            var c = new SqliteConnection("Data Source=:memory:");
            c.Open();
            return (c, new SqliteDialect());
        }

        [Fact]
        public async Task OneToOne_includeRelated_handles_more_than_500_fk_values()
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

            const int count = 501;
            for (int i = 0; i < count; i++)
            {
                var address = await addresses.SaveAsync(new AddressModel { Street = $"Street {i}", City = "Chunk" });
                await students.SaveAsync(new StudentModel { Name = $"Student {i}", AddressId = address.Id });
            }

            var loaded = await students.FindAllAsync(includeRelated: true);

            Assert.Equal(count, loaded.Count);
            Assert.All(loaded, student =>
            {
                Assert.NotNull(student.Address);
                Assert.StartsWith("Street ", student.Address.Street);
            });
        }

        [Fact]
        public async Task OneToMany_includeRelated_handles_more_than_500_root_ids()
        {
            var (conn, dialect) = Open();
            var parents = new SqlRepository<ParentModel, int>(conn, dialect);
            var children = new SqlRepository<ChildModel, int>(conn, dialect);
            await parents.EnsureSchemaAsync();
            await children.EnsureSchemaAsync();

            const int count = 501;
            for (int i = 0; i < count; i++)
            {
                var parent = await parents.SaveAsync(new ParentModel { Name = $"Parent {i}" });
                await children.SaveAsync(new ChildModel { Label = $"Child {i}", ParentId = parent.Id });
            }

            var loaded = await parents.FindAllAsync(includeRelated: true);

            Assert.Equal(count, loaded.Count);
            Assert.All(loaded, parent =>
            {
                Assert.NotNull(parent.Children);
                Assert.Single(parent.Children);
            });
        }

        [Fact]
        public async Task ManyToMany_includeRelated_handles_more_than_500_root_ids()
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

            const int count = 501;
            var sharedCourse = await courses.SaveAsync(new CourseModel { Name = "Stress Course" });
            for (int i = 0; i < count; i++)
            {
                var student = await students.SaveAsync(new StudentModel { Name = $"Student {i}" });
                await links.SaveAsync(new StudentCourseLink { StudentId = student.Id, CourseId = sharedCourse.Id });
            }

            var loaded = await students.FindAllAsync(includeRelated: true);

            Assert.Equal(count, loaded.Count);
            Assert.All(loaded, student =>
            {
                Assert.NotNull(student.Courses);
                Assert.Single(student.Courses);
                Assert.Equal("Stress Course", student.Courses[0].Name);
            });
            Assert.Equal(count, loaded.SelectMany(s => s.Courses).Count());
        }
    }
}
