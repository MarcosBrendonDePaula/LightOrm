using System;

namespace LightOrm.Core.Attributes
{
    /// <summary>
    /// Cria um índice na coluna durante EnsureSchemaAsync. Quando Unique=true,
    /// emite UNIQUE INDEX. Múltiplas propriedades com o mesmo Name compartilham
    /// um índice composto na ordem em que aparecem (índices nomeados são
    /// estáveis pela ordem de PropertyInfo do tipo).
    ///
    /// Mongo ignora este atributo neste momento — índices Mongo serão tratados
    /// em PR futuro.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class IndexAttribute : Attribute
    {
        public string Name { get; }
        public bool Unique { get; }

        public IndexAttribute(string name = null, bool unique = false)
        {
            Name = name;
            Unique = unique;
        }
    }

    /// <summary>
    /// Atalho para [Index(unique: true)]. Emite UNIQUE INDEX dedicado para
    /// a coluna. Use [Index(name: \"...\", unique: true)] para índice nomeado.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class UniqueAttribute : Attribute
    {
    }
}
