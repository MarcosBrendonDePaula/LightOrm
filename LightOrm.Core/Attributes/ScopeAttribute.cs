using System;

namespace LightOrm.Core.Attributes
{
    /// <summary>
    /// Marca um método estático do modelo como scope nomeado. O método deve ter
    /// a assinatura: public static IQuery&lt;T,TId&gt; NomeDoScope(IQuery&lt;T,TId&gt; q).
    ///
    /// Uso: repo.Scope("active").Where(...).ToListAsync().
    /// Vários scopes encadeiam: repo.Scope("active").Scope("recent")...
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public class ScopeAttribute : Attribute
    {
        public string Name { get; }
        public ScopeAttribute(string name) => Name = name;
    }
}
