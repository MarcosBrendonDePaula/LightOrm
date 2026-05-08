using System;
using System.Data.Common;
using LightOrm.Core.Models;
using LightOrm.Core.Sql;

namespace LightOrm.Core
{
    /// <summary>
    /// Helpers para construir IRepository<T,TId> sem depender da classe concreta
    /// no código de negócio. O caller passa connection + dialect (SQL) ou um
    /// objeto Mongo-equivalente, e recebe a abstração.
    ///
    /// MongoRepository não está exposto aqui porque vive em outro assembly e
    /// importaria MongoDB.Driver no Core. Use a sobrecarga neutra que aceita
    /// uma fábrica injetada (ver UnitTests para exemplo) ou instancie
    /// MongoRepository diretamente no composition root.
    /// </summary>
    public static class RepositoryFactory
    {
        public static IRepository<T, TId> Sql<T, TId>(DbConnection connection, IDialect dialect,
            DbTransaction ambientTx = null)
            where T : BaseModel<T, TId>, new()
        {
            return new SqlRepository<T, TId>(connection, dialect, ambientTx);
        }

        public static IRepository<T, TId> Create<T, TId>(Func<IRepository<T, TId>> factory)
            where T : BaseModel<T, TId>, new()
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            return factory();
        }
    }
}
