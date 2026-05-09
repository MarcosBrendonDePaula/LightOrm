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

    public class CascadeDeleteParentModel : BaseModel<CascadeDeleteParentModel, int>
    {
        public override string TableName => "cascade_delete_parent";
        [Column("name", length: 100)] public string Name { get; set; }
        [OneToMany("parent_id", typeof(CascadeChildModel), cascade: false, cascadeDelete: true)]
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

    public class HookTrackingModel : BaseModel<HookTrackingModel, int>
    {
        public override string TableName => "hook_tracking";

        [Column("name", length: 50)]
        public string Name { get; set; }

        // Não [Column] — é só estado em memória pra teste verificar.
        public System.Collections.Generic.List<string> Events { get; set; }
            = new System.Collections.Generic.List<string>();

        protected internal override void OnBeforeSave(bool isInsert)
            => Events.Add(isInsert ? "before-insert" : "before-update");
        protected internal override void OnAfterSave(bool isInsert)
            => Events.Add(isInsert ? "after-insert" : "after-update");
        protected internal override void OnBeforeDelete() => Events.Add("before-delete");
        protected internal override void OnAfterDelete() => Events.Add("after-delete");
        protected internal override void OnAfterLoad() => Events.Add("after-load");
    }

    [SoftDelete]
    public class GranularHookModel : BaseModel<GranularHookModel, int>
    {
        public override string TableName => "granular_hook";

        [Column("name", length: 50)] public string Name { get; set; }

        public System.Collections.Generic.List<string> Events { get; set; }
            = new System.Collections.Generic.List<string>();

        public bool BlockSave { get; set; }
        public bool BlockDelete { get; set; }

        protected internal override void OnBeforeCreate() => Events.Add("before-create");
        protected internal override void OnAfterCreate()  => Events.Add("after-create");
        protected internal override void OnBeforeUpdate() => Events.Add("before-update");
        protected internal override void OnAfterUpdate()  => Events.Add("after-update");
        protected internal override void OnBeforeValidate() => Events.Add("before-validate");
        protected internal override void OnAfterValidate()  => Events.Add("after-validate");
        protected internal override void OnBeforeRestore() => Events.Add("before-restore");
        protected internal override void OnAfterRestore()  => Events.Add("after-restore");

        protected internal override Task<bool> CanSaveAsync(bool isInsert)
            => Task.FromResult(!BlockSave);
        protected internal override Task<bool> CanDeleteAsync()
            => Task.FromResult(!BlockDelete);
    }

    public class ValidatedModel : BaseModel<ValidatedModel, int>
    {
        public override string TableName => "validated";

        [Column("email", length: 255)]
        [LightOrm.Core.Validation.Required]
        [LightOrm.Core.Validation.RegEx(@"^[^@]+@[^@]+$")]
        public string Email { get; set; }

        [Column("nickname", length: 50)]
        [LightOrm.Core.Validation.MinLength(3)]
        [LightOrm.Core.Validation.MaxLength(20)]
        public string Nickname { get; set; }

        [Column("age")]
        [LightOrm.Core.Validation.Range(0, 150)]
        public int Age { get; set; }
    }

    public class ScopedModel : BaseModel<ScopedModel, int>
    {
        public override string TableName => "scoped";

        [Column("name", length: 50)] public string Name { get; set; }
        [Column("active")]            public bool Active { get; set; }
        [Column("priority")]          public int Priority { get; set; }

        [Scope("active")]
        public static IQuery<ScopedModel, int> ScopeActive(IQuery<ScopedModel, int> q) =>
            q.Where("Active", true);

        [Scope("highPriority")]
        public static IQuery<ScopedModel, int> ScopeHighPriority(IQuery<ScopedModel, int> q) =>
            q.Where("Priority", ">=", 5);

        [Scope("byName")]
        public static IQuery<ScopedModel, int> ScopeByName(IQuery<ScopedModel, int> q) =>
            q.OrderBy("Name");
    }

    public class GrandparentModel : BaseModel<GrandparentModel, int>
    {
        public override string TableName => "grandparent";
        [Column("name", length: 100)] public string Name { get; set; }
        [OneToMany("grandparent_id", typeof(MidParentModel))]
        public MidParentModel[] Mids { get; set; }
    }

    public class MidParentModel : BaseModel<MidParentModel, int>
    {
        public override string TableName => "midparent";
        [Column("label", length: 100)] public string Label { get; set; }
        [Column("grandparent_id")] public int GrandparentId { get; set; }
        [OneToMany("midparent_id", typeof(LeafModel))]
        public LeafModel[] Leaves { get; set; }
    }

    public class LeafModel : BaseModel<LeafModel, int>
    {
        public override string TableName => "leaf";
        [Column("value", length: 100)] public string Value { get; set; }
        [Column("midparent_id")] public int MidparentId { get; set; }
    }

    [SoftDelete]
    public class SoftDeletedModel : BaseModel<SoftDeletedModel, int>
    {
        public override string TableName => "soft_deleted";

        [Column("name", length: 100)]
        public string Name { get; set; }
    }

    [Table("table_attr_demo")]
    public class TableAttributeModel : BaseModel<TableAttributeModel, int>
    {
        // Não sobrescreve TableName — vem do [Table].
        [Column("name", length: 50)]
        public string Name { get; set; }
    }

    public class IndexedModel : BaseModel<IndexedModel, int>
    {
        public override string TableName => "indexed_model";

        [Column("email", length: 200)]
        [Unique]
        public string Email { get; set; }

        // Índice composto: ambas as propriedades compartilham o mesmo Name.
        [Column("first", length: 50)]
        [Index("idx_full_name")]
        public string First { get; set; }

        [Column("last", length: 50)]
        [Index("idx_full_name")]
        public string Last { get; set; }

        // Índice anônimo simples.
        [Column("created_year")]
        [Index]
        public int CreatedYear { get; set; }
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
