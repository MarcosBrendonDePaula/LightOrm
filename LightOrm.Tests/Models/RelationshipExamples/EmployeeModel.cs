using System;
using LightOrm.Core.Models;
using LightOrm.Core.Attributes;

namespace LightOrm.Tests.Models.RelationshipExamples
{
    public class EmployeeModel : BaseModel<EmployeeModel>
    {
        public override string TableName => "employees";

        [Column("first_name", length: 50)]
        public string FirstName { get; set; }

        [Column("last_name", length: 50)]
        public string LastName { get; set; }

        [Column("email", length: 255)]
        public string Email { get; set; }

        [Column("hire_date")]
        public DateTime HireDate { get; set; }

        [Column("salary")]
        public decimal Salary { get; set; }

        [Column("department_id")]
        [ForeignKey("departments", "Id")]
        public int? DepartmentId { get; set; }

        // Navigation property back to Department
        [OneToOne("DepartmentId", typeof(DepartmentModel))]
        public DepartmentModel Department { get; set; }

        // One-to-Many relationship with supervised employees (self-referencing)
        [Column("supervisor_id")]
        [ForeignKey("employees", "Id")]
        public int? SupervisorId { get; set; }

        [OneToOne("SupervisorId", typeof(EmployeeModel))]
        public EmployeeModel Supervisor { get; set; }

        [OneToMany("supervisor_id", typeof(EmployeeModel))]
        public EmployeeModel[] Subordinates { get; set; }

        // Many-to-Many relationship with Projects
        [ManyToMany(
            typeof(ProjectModel),
            associationTable: "employee_projects",
            sourceForeignKey: "employee_id",
            targetForeignKey: "project_id")]
        public ProjectModel[] Projects { get; set; }
    }
}
