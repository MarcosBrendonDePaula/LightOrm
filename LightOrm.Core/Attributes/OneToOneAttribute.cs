using System;

namespace LightOrm.Core.Attributes
{
    /// <summary>
    /// Attribute to define a one-to-one relationship in a database model.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class OneToOneAttribute : Attribute
    {
        public string ForeignKeyProperty { get; }
        public Type RelatedType { get; }

        public OneToOneAttribute(string foreignKeyProperty, Type relatedType)
        {
            ForeignKeyProperty = foreignKeyProperty;
            RelatedType = relatedType;
        }
    }
}
