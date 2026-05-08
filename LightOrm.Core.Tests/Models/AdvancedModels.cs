using System;
using LightOrm.Core.Attributes;
using LightOrm.Core.Models;

namespace LightOrm.Core.Tests.Models
{
    public class TypesModel : BaseModel<TypesModel, int>
    {
        public override string TableName => "types_model";

        [Column("name", length: 200)]
        public string Name { get; set; }

        [Column("guid_value")]
        public Guid GuidValue { get; set; }

        [Column("decimal_value")]
        public decimal DecimalValue { get; set; }

        [Column("date_value")]
        public DateTime DateValue { get; set; }

        [Column("nullable_int")]
        public int? NullableInt { get; set; }

        [Column("nullable_date")]
        public DateTime? NullableDate { get; set; }
    }

    public class ParentModel : BaseModel<ParentModel, int>
    {
        public override string TableName => "adv_parent";

        [Column("name", length: 100)]
        public string Name { get; set; }

        [OneToMany("parent_id", typeof(ChildModel))]
        public ChildModel[] Children { get; set; }
    }

    public class ChildModel : BaseModel<ChildModel, int>
    {
        public override string TableName => "adv_child";

        [Column("label", length: 100)]
        public string Label { get; set; }

        [Column("parent_id")]
        public int? ParentId { get; set; }
    }

    public class CascadeParentModel : BaseModel<CascadeParentModel, int>
    {
        public override string TableName => "cascade_parent";

        [Column("name", length: 100)]
        public string Name { get; set; }

        [OneToMany("parent_id", typeof(CascadeChildModel), cascade: true)]
        public CascadeChildModel[] Children { get; set; }
    }

    public class CascadeChildModel : BaseModel<CascadeChildModel, int>
    {
        public override string TableName => "cascade_child";

        [Column("label", length: 100)]
        public string Label { get; set; }

        [Column("parent_id")]
        public int ParentId { get; set; }
    }

    public class VersionedModel : BaseModel<VersionedModel, int>
    {
        public override string TableName => "versioned";

        [Column("name", length: 100)]
        public string Name { get; set; }

        [Column("row_version")]
        [Version]
        public int RowVersion { get; set; }
    }
}
