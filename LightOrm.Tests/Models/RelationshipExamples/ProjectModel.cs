using System;
using LightOrm.Core.Models;
using LightOrm.Core.Attributes;

namespace LightOrm.Tests.Models.RelationshipExamples
{
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
}
