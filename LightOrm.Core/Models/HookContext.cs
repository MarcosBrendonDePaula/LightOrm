using System;
using System.Collections.Concurrent;
using System.Data.Common;
using System.Reflection;
using LightOrm.Core.Sql;

namespace LightOrm.Core.Models
{
    /// <summary>
    /// Contexto opcional passado aos hooks de ciclo de vida do BaseModel.
    /// Permite ao hook executar side-effects multi-tabela (ex.: audit log)
    /// na MESMA transação do save/delete em curso — se a operação principal
    /// for revertida, o audit também é.
    ///
    /// Use GetRepository&lt;TOther, TIdOther&gt;() para obter um SqlRepository
    /// configurado com a connection + dialect + tx ambiente atuais.
    ///
    /// HookContext só é fornecido em hooks de SqlRepository. Para hooks
    /// no MongoRepository, o contexto é null (Mongo tem semântica diferente
    /// de transação multi-coleção).
    /// </summary>
    public sealed class HookContext
    {
        public DbConnection Connection { get; }
        public DbTransaction Transaction { get; }
        public IDialect Dialect { get; }

        // Cache por (T, TId) para não recriar SqlRepository a cada call.
        private readonly ConcurrentDictionary<(Type, Type), object> _repos
            = new ConcurrentDictionary<(Type, Type), object>();

        public HookContext(DbConnection connection, IDialect dialect, DbTransaction transaction)
        {
            Connection = connection ?? throw new ArgumentNullException(nameof(connection));
            Dialect = dialect ?? throw new ArgumentNullException(nameof(dialect));
            Transaction = transaction;
        }

        /// <summary>
        /// Devolve um SqlRepository&lt;TOther, TIdOther&gt; ligado à mesma
        /// connection/dialect/tx ambiente. Operações disparadas nele
        /// participam da transação em curso.
        /// </summary>
        public SqlRepository<TOther, TIdOther> GetRepository<TOther, TIdOther>()
            where TOther : BaseModel<TOther, TIdOther>, new()
        {
            var key = (typeof(TOther), typeof(TIdOther));
            return (SqlRepository<TOther, TIdOther>)_repos.GetOrAdd(key,
                _ => new SqlRepository<TOther, TIdOther>(Connection, Dialect, Transaction));
        }
    }
}
