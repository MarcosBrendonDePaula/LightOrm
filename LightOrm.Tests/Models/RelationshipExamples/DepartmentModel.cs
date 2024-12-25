using System;
using LightOrm.Core.Models;
using LightOrm.Core.Attributes;

namespace LightOrm.Tests.Models.RelationshipExamples
{
    public class DepartmentModel : BaseModel<DepartmentModel>
    {
        public override string TableName => "departments";

        [Column("name", length: 100)]
        public string Name { get; set; }

        [Column("budget")]
        public decimal Budget { get; set; }

        [Column("location", length: 100)]
        public string Location { get; set; }

        // One-to-Many relationship with employees
        [OneToMany("department_id", typeof(EmployeeModel))]
        public EmployeeModel[] Employees { get; set; }

        // One-to-One relationship with department head
        [Column("head_employee_id")]
        [ForeignKey("employees", "Id")]
        public int? HeadEmployeeId { get; set; }

        [OneToOne("HeadEmployeeId", typeof(EmployeeModel))]
        public EmployeeModel DepartmentHead { get; set; }
    }

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

    public class ProjectModel : BaseModel<ProjectModel>
    {
        public override string TableName => "projects";

        [Column("name", length: 100)]
        public string Name { get; set; }

        [Column("description", length: 1000)]
        public string Description { get; set; }

        [Column("start_date")]
        public DateTime StartDate { get; set; }

        [Column("end_date")]
        public DateTime? EndDate { get; set; }

        [Column("budget")]
        public decimal Budget { get; set; }

        // Many-to-Many relationship with Employees
        [ManyToMany(
            typeof(EmployeeModel),
            associationTable: "employee_projects",
            sourceForeignKey: "project_id",
            targetForeignKey: "employee_id")]
        public EmployeeModel[] TeamMembers { get; set; }
    }

    // Custom association table for Employee-Project relationship
    public class EmployeeProjectModel : BaseModel<EmployeeProjectModel>
    {
        public override string TableName => "employee_projects";

        [Column("employee_id")]
        [ForeignKey("employees", "Id")]
        public int EmployeeId { get; set; }

        [Column("project_id")]
        [ForeignKey("projects", "Id")]
        public int ProjectId { get; set; }

        [Column("role", length: 50)]
        public string Role { get; set; }

        [Column("hours_allocated")]
        public int HoursAllocated { get; set; }

        [Column("start_date")]
        public DateTime StartDate { get; set; }

        [Column("end_date")]
        public DateTime? EndDate { get; set; }

        // Navigation properties
        [OneToOne("EmployeeId", typeof(EmployeeModel))]
        public EmployeeModel Employee { get; set; }

        [OneToOne("ProjectId", typeof(ProjectModel))]
        public ProjectModel Project { get; set; }
    }
}
