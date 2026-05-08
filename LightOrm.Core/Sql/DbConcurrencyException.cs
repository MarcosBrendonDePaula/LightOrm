using System;

namespace LightOrm.Core.Sql
{
    /// <summary>
    /// Lançada quando um UPDATE com optimistic locking não afeta nenhuma linha —
    /// significa que outro processo modificou (ou deletou) a linha desde que
    /// a entidade foi carregada.
    /// </summary>
    public class DbConcurrencyException : Exception
    {
        public DbConcurrencyException(string message) : base(message) { }
        public DbConcurrencyException(string message, Exception inner) : base(message, inner) { }
    }
}
