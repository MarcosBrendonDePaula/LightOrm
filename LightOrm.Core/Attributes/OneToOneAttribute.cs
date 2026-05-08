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
        // Quando true, SaveAsync(pai) também salva o filho referenciado pela
        // navigation property e propaga o id apropriado para o FkProperty do
        // pai. Default false (opt-in).
        public bool Cascade { get; }

        public OneToOneAttribute(string foreignKeyProperty, Type relatedType, bool cascade = false)
        {
            ForeignKeyProperty = foreignKeyProperty;
            RelatedType = relatedType;
            Cascade = cascade;
        }
    }
}
