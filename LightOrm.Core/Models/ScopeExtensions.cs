using System;
using System.Collections.Concurrent;
using System.Reflection;
using LightOrm.Core.Attributes;

namespace LightOrm.Core.Models
{
    public static class ScopeExtensions
    {
        // Cache de scopes resolvidos por tipo (Type -> Dictionary<name, MethodInfo>).
        private static readonly ConcurrentDictionary<Type, System.Collections.Generic.Dictionary<string, MethodInfo>>
            _cache = new ConcurrentDictionary<Type, System.Collections.Generic.Dictionary<string, MethodInfo>>();

        /// <summary>
        /// Aplica um scope nomeado e devolve o IQuery resultante para continuar
        /// encadeando. Lança se o scope não existir, e ArgumentException se a
        /// assinatura do método não bater (deve ser
        /// public static IQuery&lt;T,TId&gt; (IQuery&lt;T,TId&gt;)).
        /// </summary>
        public static IQuery<T, TId> Scope<T, TId>(this IRepository<T, TId> repo, string scopeName)
            where T : BaseModel<T, TId>, new()
        {
            return repo.Query().Scope(scopeName);
        }

        /// <summary>Encadeia outro scope sobre um IQuery existente.</summary>
        public static IQuery<T, TId> Scope<T, TId>(this IQuery<T, TId> query, string scopeName)
            where T : BaseModel<T, TId>, new()
        {
            if (string.IsNullOrEmpty(scopeName))
                throw new ArgumentException("Nome de scope vazio.", nameof(scopeName));

            var scopes = ResolveScopes(typeof(T));
            if (!scopes.TryGetValue(scopeName, out var method))
                throw new ArgumentException(
                    $"Scope '{scopeName}' não encontrado em {typeof(T).Name}. " +
                    $"Defina um método estático com [Scope(\"{scopeName}\")].",
                    nameof(scopeName));

            var result = method.Invoke(null, new object[] { query });
            if (result is IQuery<T, TId> q) return q;
            throw new InvalidOperationException(
                $"Scope '{scopeName}' em {typeof(T).Name} não retornou IQuery<{typeof(T).Name},{typeof(TId).Name}>.");
        }

        private static System.Collections.Generic.Dictionary<string, MethodInfo> ResolveScopes(Type modelType)
        {
            return _cache.GetOrAdd(modelType, t =>
            {
                var dict = new System.Collections.Generic.Dictionary<string, MethodInfo>(StringComparer.Ordinal);
                foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Static))
                {
                    var attr = m.GetCustomAttribute<ScopeAttribute>(inherit: true);
                    if (attr == null) continue;

                    var parameters = m.GetParameters();
                    if (parameters.Length != 1)
                        throw new InvalidOperationException(
                            $"[Scope(\"{attr.Name}\")] em {t.Name}.{m.Name}: deve receber exatamente um IQuery.");
                    dict[attr.Name] = m;
                }
                return dict;
            });
        }
    }
}
