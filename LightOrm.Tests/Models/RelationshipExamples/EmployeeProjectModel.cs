using System;
using LightOrm.Core.Models;
using LightOrm.Core.Attributes;

namespace LightOrm.Tests.Models.RelationshipExamples
{
    public class EmployeeProjectModel : BaseModel<EmployeeProjectModel>
    {
        [Column("employee_id")]
        [ForeignKey("employees", "Id")]
        public int EmployeeId { get; set; }

        [Column("project_id")]
        [ForeignKey("projects", "Id")]
        public int ProjectId { get; set; }

        [Column("role")]
        public string Role { get; set; }

        [Column("hours_allocated")]
        public int HoursAllocated { get; set; }

        [Column("start_date")]
        public DateTime StartDate { get; set; }

        [Column("end_date")]
        public DateTime EndDate { get; set; }

        public override string TableName => "employee_projects";

        // Navigation properties
        [OneToOne("EmployeeId", typeof(EmployeeModel))]
        public EmployeeModel Employee { get; set; }

        [OneToOne("ProjectId", typeof(ProjectModel))]
        public ProjectModel Project { get; set; }
    }
}
