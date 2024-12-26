using System;
using LightOrm.Core.Models;
using LightOrm.Core.Attributes;

namespace LightOrm.Tests.Models
{
    public class ConfigurationTestModel : BaseModel<ConfigurationTestModel>
    {
        public override string TableName => "configuration_tests";

        // Basic column with length and nullability
        [Column("name", length: 100, isNullable: false)]
        public string Name { get; set; }

        // Unique column with index
        [Column("email", length: 255, isNullable: false, isUnique: true, hasIndex: true)]
        public string Email { get; set; }

        // Enum-like column with check constraint
        [Column("status", length: 20, isNullable: false, defaultValue: "active",
                enumValues: new[] { "active", "inactive", "pending", "deleted" })]
        public string Status { get; set; }

        // Decimal column with precision and scale
        [Column("balance", isNullable: false, defaultValue: 0.0, precision: 10, scale: 2)]
        public decimal Balance { get; set; }

        // Unsigned integer with check constraint
        [Column("age", isNullable: false, defaultValue: 0, isUnsigned: true,
                checkConstraint: "age >= 0 AND age <= 150")]
        public int Age { get; set; }

        // Boolean with default value
        [Column("is_active", isNullable: false, defaultValue: true)]
        public bool IsActive { get; set; }

        // Nullable datetime
        [Column("last_login", isNullable: true)]
        public DateTime? LastLogin { get; set; }

        // String with default value
        [Column("notes", length: 1000, isNullable: true, defaultValue: "")]
        public string Notes { get; set; }

        // Custom check constraint
        [Column("score", isNullable: false, defaultValue: 0,
                checkConstraint: "score >= 0 AND score <= 100")]
        public int Score { get; set; }
    }
}
