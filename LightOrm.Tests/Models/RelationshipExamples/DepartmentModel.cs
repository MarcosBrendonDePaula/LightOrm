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
}
