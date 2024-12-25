using System;
using LightOrm.Core.Models;
using LightOrm.Core.Attributes;

namespace LightOrm.Tests.Models.RelationshipExamples
{
    public class StudentModel : BaseModel<StudentModel>
    {
        public override string TableName => "students";

        [Column("name", length: 100)]
        public string Name { get; set; }

        [Column("email", length: 255)]
        public string Email { get; set; }

        [Column("address_id")]
        [ForeignKey("addresses", "Id")]
        public int? AddressId { get; set; }

        // Navigation property for one-to-one relationship with Address
        [OneToOne("AddressId", typeof(AddressModel))]
        public AddressModel Address { get; set; }

        // Many-to-Many relationship with Course, using custom association table
        [ManyToMany(
            typeof(CourseModel),
            associationTable: "student_courses",
            sourceForeignKey: "student_id",
            targetForeignKey: "course_id")]
        public CourseModel[] Courses { get; set; }

        // One-to-Many relationship with Assignment
        [OneToMany("student_id", typeof(AssignmentModel))]
        public AssignmentModel[] Assignments { get; set; }
    }

    public class AddressModel : BaseModel<AddressModel>
    {
        public override string TableName => "addresses";

        [Column("street", length: 200)]
        public string Street { get; set; }

        [Column("city", length: 100)]
        public string City { get; set; }

        [Column("state", length: 50)]
        public string State { get; set; }

        [Column("postal_code", length: 20)]
        public string PostalCode { get; set; }

        // One-to-One back reference to Student
        [OneToOne("Id", typeof(StudentModel))]
        public StudentModel Student { get; set; }
    }

    public class CourseModel : BaseModel<CourseModel>
    {
        public override string TableName => "courses";

        [Column("code", length: 20)]
        public string Code { get; set; }

        [Column("name", length: 100)]
        public string Name { get; set; }

        [Column("credits")]
        public int Credits { get; set; }

        // Many-to-Many relationship with Student
        [ManyToMany(
            typeof(StudentModel),
            associationTable: "student_courses",
            sourceForeignKey: "course_id",
            targetForeignKey: "student_id")]
        public StudentModel[] Students { get; set; }
    }

    // Custom association table model for Student-Course relationship
    public class StudentCourseModel : BaseModel<StudentCourseModel>
    {
        public override string TableName => "student_courses";

        [Column("student_id")]
        [ForeignKey("students", "Id")]
        public int StudentId { get; set; }

        [Column("course_id")]
        [ForeignKey("courses", "Id")]
        public int CourseId { get; set; }

        [Column("enrollment_date")]
        public DateTime EnrollmentDate { get; set; }

        [Column("grade", length: 2)]
        public string Grade { get; set; }

        // Navigation properties
        [OneToOne("StudentId", typeof(StudentModel))]
        public StudentModel Student { get; set; }

        [OneToOne("CourseId", typeof(CourseModel))]
        public CourseModel Course { get; set; }
    }

    public class AssignmentModel : BaseModel<AssignmentModel>
    {
        public override string TableName => "assignments";

        [Column("title", length: 100)]
        public string Title { get; set; }

        [Column("description", length: 1000)]
        public string Description { get; set; }

        [Column("due_date")]
        public DateTime DueDate { get; set; }

        [Column("score")]
        public decimal Score { get; set; }

        [Column("student_id")]
        [ForeignKey("students", "Id")]
        public int StudentId { get; set; }

        // Navigation property back to Student
        [OneToOne("StudentId", typeof(StudentModel))]
        public StudentModel Student { get; set; }
    }
}
