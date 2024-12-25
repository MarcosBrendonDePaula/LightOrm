using System;

namespace LightOrm.Core.Attributes
{
    /// <summary>
    /// Attribute to define a one-to-many relationship in a database model.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class OneToManyAttribute : Attribute
    {
        public string ForeignKeyProperty { get; }
        public Type RelatedType { get; }

        public OneToManyAttribute(string foreignKeyProperty, Type relatedType)
        {
            ForeignKeyProperty = foreignKeyProperty;
            RelatedType = relatedType;
        }
    }
}
