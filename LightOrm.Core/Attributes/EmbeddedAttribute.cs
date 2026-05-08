using System;

namespace LightOrm.Core.Attributes
{
    /// <summary>
    /// Marca uma propriedade como subdocumento aninhado em bancos document
    /// (ex.: MongoDB). O tipo subjacente pode ser uma classe POCO (1:1) ou
    /// um array/lista de POCOs (1:N). Subdocumentos não precisam herdar
    /// BaseModel — apenas usar [Column] nos campos persistidos.
    ///
    /// Em SqlRepository, [Embedded] é ignorado: a propriedade não vira coluna.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class EmbeddedAttribute : Attribute
    {
    }
}
