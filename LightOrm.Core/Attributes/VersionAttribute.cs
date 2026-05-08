using System;

namespace LightOrm.Core.Attributes
{
    /// <summary>
    /// Marca uma coluna inteira como versão otimista. O SqlRepository:
    ///   - inicia o valor em 1 no INSERT;
    ///   - incrementa em 1 a cada UPDATE;
    ///   - inclui WHERE version = @oldVersion no UPDATE — se nenhuma linha
    ///     for afetada, lança DbConcurrencyException.
    ///
    /// A propriedade ainda precisa de [Column(...)] declarando nome/tipo.
    /// Tipos suportados: int, long.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class VersionAttribute : Attribute
    {
    }
}
