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
        // Quando true, SaveAsync(pai) propaga o id pra FK dos filhos no array
        // e salva todos numa única transação. Default false (opt-in).
        public bool Cascade { get; }

        public OneToManyAttribute(string foreignKeyProperty, Type relatedType, bool cascade = false)
        {
            ForeignKeyProperty = foreignKeyProperty;
            RelatedType = relatedType;
            Cascade = cascade;
        }
    }
}
