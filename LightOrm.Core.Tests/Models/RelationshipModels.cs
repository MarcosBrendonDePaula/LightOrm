using LightOrm.Core.Attributes;
using LightOrm.Core.Models;

namespace LightOrm.Core.Tests.Models
{
    public class AddressModel : BaseModel<AddressModel, int>
    {
        public override string TableName => "addresses";

        [Column("street", length: 200)]
        public string Street { get; set; }

        [Column("city", length: 100)]
        public string City { get; set; }
    }

    public class StudentModel : BaseModel<StudentModel, int>
    {
        public override string TableName => "students";

        [Column("name", length: 100)]
        public string Name { get; set; }

        [Column("address_id")]
        [ForeignKey("addresses")]
        public int? AddressId { get; set; }

        [OneToOne("AddressId", typeof(AddressModel))]
        public AddressModel Address { get; set; }

        [OneToMany("student_id", typeof(AssignmentModel))]
        public AssignmentModel[] Assignments { get; set; }

        [ManyToMany(
            typeof(CourseModel),
            associationTable: "student_courses",
            sourceForeignKey: "student_id",
            targetForeignKey: "course_id")]
        public CourseModel[] Courses { get; set; }
    }

    public class AssignmentModel : BaseModel<AssignmentModel, int>
    {
        public override string TableName => "assignments";

        [Column("title", length: 200)]
        public string Title { get; set; }

        [Column("student_id")]
        [ForeignKey("students")]
        public int StudentId { get; set; }
    }

    public class CourseModel : BaseModel<CourseModel, int>
    {
        public override string TableName => "courses";

        [Column("name", length: 100)]
        public string Name { get; set; }
    }

    public class StudentCourseLink : BaseModel<StudentCourseLink, int>
    {
        public override string TableName => "student_courses";

        [Column("student_id")]
        public int StudentId { get; set; }

        [Column("course_id")]
        public int CourseId { get; set; }
    }
}
