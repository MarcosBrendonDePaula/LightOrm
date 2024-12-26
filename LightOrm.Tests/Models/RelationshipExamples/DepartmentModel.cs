using System;
using LightOrm.Core.Models;
using LightOrm.Core.Attributes;

namespace LightOrm.Tests.Models.RelationshipExamples
{
    public class DepartmentModel : BaseModel<DepartmentModel>
    {
        public override string TableName => "departments";

        [Column("name", length: 100, isNullable: false)]
        public string Name { get; set; }

        [Column("code", length: 20, isNullable: false)]
        public string Code { get; set; }

        [Column("budget", isNullable: false, defaultValue: 0.0)]
        public decimal Budget { get; set; }

        [Column("location", length: 100, isNullable: false)]
        public string Location { get; set; }

        [Column("description", length: 500, isNullable: true, defaultValue: "")]
        public string Description { get; set; }

        [Column("is_active", isNullable: false, defaultValue: true)]
        public bool IsActive { get; set; }

        [Column("established_date", isNullable: false, defaultValue: "CURRENT_TIMESTAMP")]
        public DateTime EstablishedDate { get; set; }

        // One-to-Many relationship with employees
        [OneToMany("department_id", typeof(EmployeeModel))]
        public EmployeeModel[] Employees { get; set; }

        // One-to-One relationship with department head
        [Column("head_employee_id", isNullable: true)]
        [ForeignKey("employees", "Id")]
        public int? HeadEmployeeId { get; set; }

        [OneToOne("HeadEmployeeId", typeof(EmployeeModel))]
        public EmployeeModel DepartmentHead { get; set; }
    }
}
