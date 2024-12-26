using System;
using LightOrm.Core.Models;
using LightOrm.Core.Attributes;

namespace LightOrm.Tests.Models.RelationshipExamples
{
    public class ProjectModel : BaseModel<ProjectModel>
    {
        public override string TableName => "projects";

        [Column("name", length: 100, isNullable: false)]
        public string Name { get; set; }

        [Column("description", length: 1000, isNullable: true, defaultValue: "")]
        public string Description { get; set; }

        [Column("start_date", isNullable: false)]
        public DateTime StartDate { get; set; } = DateTime.UtcNow;

        [Column("end_date", isNullable: true)]
        public DateTime? EndDate { get; set; }

        [Column("budget", isNullable: false, defaultValue: 0.0)]
        public decimal Budget { get; set; }

        [Column("status", length: 20, isNullable: false, defaultValue: "planning")]
        public string Status { get; set; }

        [Column("priority", length: 10, isNullable: false, defaultValue: "medium")]
        public string Priority { get; set; }

        [Column("completion_percentage", isNullable: false, defaultValue: 0)]
        public int CompletionPercentage { get; set; }

        // Many-to-Many relationship with Employees
        [ManyToMany(
            typeof(EmployeeModel),
            associationTable: "employee_projects",
            sourceForeignKey: "project_id",
            targetForeignKey: "employee_id")]
        public EmployeeModel[] TeamMembers { get; set; }
    }
}
