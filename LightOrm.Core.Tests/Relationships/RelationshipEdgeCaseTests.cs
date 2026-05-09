using System.Threading.Tasks;
using LightOrm.Core.Attributes;
using LightOrm.Core.Models;
using LightOrm.Core.Sql;
using LightOrm.Sqlite;
using Microsoft.Data.Sqlite;

namespace LightOrm.Core.Tests
{
    public class RelationshipEdgeCaseTests
    {
        private static (SqliteConnection conn, IDialect dialect) Open()
        {
            var c = new SqliteConnection("Data Source=:memory:");
            c.Open();
            return (c, new SqliteDialect());
        }

        [Fact]
        public async Task OneToOne_with_orphan_fk_leaves_navigation_null()
        {
            var (conn, dialect) = Open();
            var addresses = new SqlRepository<OrphanAddressModel, int>(conn, dialect);
            var courses = new SqlRepository<OrphanCourseModel, int>(conn, dialect);
            var links = new SqlRepository<OrphanStudentCourseLinkModel, int>(conn, dialect);
            var repo = new SqlRepository<OrphanStudentModel, int>(conn, dialect);
            await addresses.EnsureSchemaAsync();
            await courses.EnsureSchemaAsync();
            await links.EnsureSchemaAsync();
            await repo.EnsureSchemaAsync();

            var entity = await repo.SaveAsync(new OrphanStudentModel { Name = "ana", AddressId = 999 });
            var loaded = await repo.FindByIdAsync(entity.Id, includeRelated: true);

            Assert.NotNull(loaded);
            Assert.Null(loaded.Address);
        }

        [Fact]
        public async Task ManyToMany_ignores_orphan_association_rows()
        {
            var (conn, dialect) = Open();
            var students = new SqlRepository<OrphanStudentModel, int>(conn, dialect);
            var courses = new SqlRepository<OrphanCourseModel, int>(conn, dialect);
            var links = new SqlRepository<OrphanStudentCourseLinkModel, int>(conn, dialect);
            await courses.EnsureSchemaAsync();
            await students.EnsureSchemaAsync();
            await links.EnsureSchemaAsync();

            var student = await students.SaveAsync(new OrphanStudentModel { Name = "bia" });
            var course = await courses.SaveAsync(new OrphanCourseModel { Name = "math" });
            await links.SaveAsync(new OrphanStudentCourseLinkModel { StudentId = student.Id, CourseId = course.Id });
            await links.SaveAsync(new OrphanStudentCourseLinkModel { StudentId = student.Id, CourseId = 999 });

            var loaded = await students.FindByIdAsync(student.Id, includeRelated: true);

            Assert.NotNull(loaded.Courses);
            Assert.Single(loaded.Courses);
            Assert.Equal("math", loaded.Courses[0].Name);
        }

        [Fact]
        public async Task Cascade_delete_soft_deletes_children_instead_of_hard_deleting()
        {
            var (conn, dialect) = Open();
            var parents = new SqlRepository<SoftCascadeParentModel, int>(conn, dialect);
            var children = new SqlRepository<SoftCascadeChildModel, int>(conn, dialect);
            await parents.EnsureSchemaAsync();
            await children.EnsureSchemaAsync();

            var parent = await parents.SaveAsync(new SoftCascadeParentModel { Name = "p" });
            var child = await children.SaveAsync(new SoftCascadeChildModel { Name = "c", ParentId = parent.Id });

            await parents.DeleteAsync(parent);

            Assert.Empty(await children.FindAllAsync());
            var allChildren = await children.FindAllIncludingDeletedAsync();
            Assert.Single(allChildren);
            Assert.Equal(child.Id, allChildren[0].Id);
            Assert.NotNull(allChildren[0].DeletedAt);
        }
    }

    public class OrphanAddressModel : BaseModel<OrphanAddressModel, int>
    {
        public override string TableName => "orphan_addresses";

        [Column("street", length: 100)]
        public string Street { get; set; }
    }

    public class OrphanCourseModel : BaseModel<OrphanCourseModel, int>
    {
        public override string TableName => "orphan_courses";

        [Column("name", length: 100)]
        public string Name { get; set; }
    }

    public class OrphanStudentCourseLinkModel : BaseModel<OrphanStudentCourseLinkModel, int>
    {
        public override string TableName => "orphan_student_courses";

        [Column("student_id")]
        public int StudentId { get; set; }

        [Column("course_id")]
        public int CourseId { get; set; }
    }

    public class OrphanStudentModel : BaseModel<OrphanStudentModel, int>
    {
        public override string TableName => "orphan_students";

        [Column("name", length: 100)]
        public string Name { get; set; }

        [Column("address_id")]
        public int? AddressId { get; set; }

        [OneToOne("AddressId", typeof(OrphanAddressModel))]
        public OrphanAddressModel Address { get; set; }

        [ManyToMany(typeof(OrphanCourseModel), "orphan_student_courses", "student_id", "course_id")]
        public OrphanCourseModel[] Courses { get; set; }
    }

    public class SoftCascadeParentModel : BaseModel<SoftCascadeParentModel, int>
    {
        public override string TableName => "soft_cascade_parent";

        [Column("name", length: 100)]
        public string Name { get; set; }

        [OneToMany("parent_id", typeof(SoftCascadeChildModel), cascadeDelete: true)]
        public SoftCascadeChildModel[] Children { get; set; }
    }

    [SoftDelete]
    public class SoftCascadeChildModel : BaseModel<SoftCascadeChildModel, int>
    {
        public override string TableName => "soft_cascade_child";

        [Column("name", length: 100)]
        public string Name { get; set; }

        [Column("parent_id")]
        public int ParentId { get; set; }
    }
}
