using System;

namespace LightOrm.Core.Attributes
{
    /// <summary>
    /// Define o nome da tabela/coleção do modelo. Alternativa idiomática a
    /// sobrescrever a propriedade abstrata TableName em BaseModel.
    /// Quando ambos existem, [Table] prevalece.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
    public class TableAttribute : Attribute
    {
        public string Name { get; }
        public TableAttribute(string name) => Name = name;
    }
}
