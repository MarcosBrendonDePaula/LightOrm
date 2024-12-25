using System;

namespace LightOrm.Core.Attributes
{
    [AttributeUsage(AttributeTargets.Property)]
    public class ManyToManyAttribute : Attribute
    {
        public Type RelatedType { get; }
        public string AssociationTable { get; }
        public string SourceForeignKey { get; }
        public string TargetForeignKey { get; }

        public ManyToManyAttribute(
            Type relatedType,
            string associationTable,
            string sourceForeignKey,
            string targetForeignKey)
        {
            RelatedType = relatedType;
            AssociationTable = associationTable;
            SourceForeignKey = sourceForeignKey;
            TargetForeignKey = targetForeignKey;
        }
    }
}
