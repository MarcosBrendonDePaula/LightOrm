using System;
using LightOrm.Core.Models;
using LightOrm.Core.Attributes;

namespace LightOrm.Tests.Models.RelationshipExamples
{
    public class EmployeeProjectModel : BaseModel<EmployeeProjectModel>
    {
        [Column("employee_id", isNullable: false)]
        [ForeignKey("employees", "Id")]
        public int EmployeeId { get; set; }

        [Column("project_id", isNullable: false)]
        [ForeignKey("projects", "Id")]
        public int ProjectId { get; set; }

        [Column("role", length: 50, isNullable: false, defaultValue: "member")]
        public string Role { get; set; }

        [Column("hours_allocated", isNullable: false, defaultValue: 40)]
        public int HoursAllocated { get; set; }

        [Column("start_date", isNullable: false)]
        public DateTime StartDate { get; set; } = DateTime.UtcNow;

        [Column("end_date", isNullable: true)]
        public DateTime? EndDate { get; set; }

        [Column("is_active", isNullable: false, defaultValue: true)]
        public bool IsActive { get; set; }

        [Column("notes", length: 500, isNullable: true, defaultValue: "")]
        public string Notes { get; set; }

        public override string TableName => "employee_projects";

        // Navigation properties
        [OneToOne("EmployeeId", typeof(EmployeeModel))]
        public EmployeeModel Employee { get; set; }

        [OneToOne("ProjectId", typeof(ProjectModel))]
        public ProjectModel Project { get; set; }
    }
}
