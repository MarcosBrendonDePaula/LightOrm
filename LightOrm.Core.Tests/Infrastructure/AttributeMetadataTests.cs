using System;
using System.Linq;
using LightOrm.Core.Attributes;
using LightOrm.Core.Utilities;

namespace LightOrm.Core.Tests
{
    public class AttributeMetadataTests
    {
        [Fact]
        public void ColumnAttribute_preserves_configuration_values()
        {
            var attr = new ColumnAttribute(
                "status",
                isPrimaryKey: true,
                autoIncrement: true,
                length: 42,
                isUnsigned: true,
                "draft",
                "published");

            Assert.Equal("status", attr.Name);
            Assert.True(attr.IsPrimaryKey);
            Assert.True(attr.AutoIncrement);
            Assert.Equal(42, attr.Length);
            Assert.True(attr.IsUnsigned);
            Assert.Equal(new[] { "draft", "published" }, attr.EnumValues);
        }

        [Fact]
        public void Relationship_attributes_preserve_constructor_arguments()
        {
            var fk = new ForeignKeyAttribute("parents", "parent_id");
            var oneToMany = new OneToManyAttribute("parent_id", typeof(AttributeTargetModel), cascade: true, cascadeDelete: true);
            var oneToOne = new OneToOneAttribute("profile_id", typeof(AttributeTargetModel), cascade: true, cascadeDelete: false);
            var manyToMany = new ManyToManyAttribute(typeof(AttributeTargetModel), "join_table", "left_id", "right_id");

            Assert.Equal("parents", fk.ReferenceTable);
            Assert.Equal("parent_id", fk.ReferenceColumn);

            Assert.Equal("parent_id", oneToMany.ForeignKeyProperty);
            Assert.Equal(typeof(AttributeTargetModel), oneToMany.RelatedType);
            Assert.True(oneToMany.Cascade);
            Assert.True(oneToMany.CascadeDelete);

            Assert.Equal("profile_id", oneToOne.ForeignKeyProperty);
            Assert.Equal(typeof(AttributeTargetModel), oneToOne.RelatedType);
            Assert.True(oneToOne.Cascade);
            Assert.False(oneToOne.CascadeDelete);

            Assert.Equal(typeof(AttributeTargetModel), manyToMany.RelatedType);
            Assert.Equal("join_table", manyToMany.AssociationTable);
            Assert.Equal("left_id", manyToMany.SourceForeignKey);
            Assert.Equal("right_id", manyToMany.TargetForeignKey);
        }

        [Fact]
        public void TypeMetadataCache_finds_navigation_and_embedded_attributes()
        {
            var props = TypeMetadataCache.GetProperties(typeof(AttributeGraphModel));

            var children = props.Single(p => p.Name == nameof(AttributeGraphModel.Children));
            var profile = props.Single(p => p.Name == nameof(AttributeGraphModel.Profile));
            var tags = props.Single(p => p.Name == nameof(AttributeGraphModel.Tags));
            var metadata = props.Single(p => p.Name == nameof(AttributeGraphModel.Metadata));

            Assert.NotNull(TypeMetadataCache.GetOneToManyAttribute(children));
            Assert.NotNull(TypeMetadataCache.GetOneToOneAttribute(profile));
            Assert.NotNull(TypeMetadataCache.GetManyToManyAttribute(tags));
            Assert.NotNull(TypeMetadataCache.GetEmbeddedAttribute(metadata));
        }

        private sealed class AttributeTargetModel
        {
        }

        private sealed class EmbeddedPayload
        {
            [Column("value")]
            public string Value { get; set; }
        }

        private sealed class AttributeGraphModel
        {
            [OneToMany("parent_id", typeof(AttributeTargetModel))]
            public AttributeTargetModel[] Children { get; set; }

            [OneToOne("profile_id", typeof(AttributeTargetModel))]
            public AttributeTargetModel Profile { get; set; }

            [ManyToMany(typeof(AttributeTargetModel), "graph_tags", "graph_id", "tag_id")]
            public AttributeTargetModel[] Tags { get; set; }

            [Embedded]
            public EmbeddedPayload Metadata { get; set; }
        }
    }
}
