using System;
using System.Collections;
using System.Data.Common;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using LightOrm.Core.Attributes;
using LightOrm.Core.Models;
using LightOrm.Core.Utilities;

namespace LightOrm.Core.Sql
{
    /// <summary>
    /// Após o INSERT/UPDATE do pai, percorre OneToOne/OneToMany com cascade=true
    /// e salva os filhos na mesma transação. ManyToMany e OneToOne reverso
    /// (FK no filho) ainda não são suportados.
    /// </summary>
    internal static class CascadeSaver
    {
        public static async Task SaveCascadesAsync(
            DbConnection connection, IDialect dialect, DbTransaction tx,
            Type rootType, object root, PropertyInfo idProp)
        {
            foreach (var prop in TypeMetadataCache.GetProperties(rootType))
            {
                var oneToMany = TypeMetadataCache.GetOneToManyAttribute(prop);
                if (oneToMany != null && oneToMany.Cascade)
                {
                    await SaveOneToManyAsync(connection, dialect, tx, root, prop, oneToMany, idProp);
                    continue;
                }

                // OneToOne com cascade tem dois sabores possíveis:
                //  1. FK no pai (caso clássico do projeto): o pai já salvou e o
                //     fkProperty é um id que aponta pra outra tabela. Cascade aqui
                //     significa "salvar o filho referenciado e atualizar fkProperty"
                //     — mas isso obrigaria DOIS inserts e um UPDATE no pai.
                //     Não suporto neste PR pra evitar ambiguidade.
                //  2. FK no filho (1:1 reverso): o filho aponta pro pai. É o caso
                //     análogo a OneToMany de um item só. Suporto este.
                //
                // Convenção: se a navigation property OneToOne é Cascade, tratamos
                // como "filho com FK pro pai" — exige que o tipo relacionado tenha
                // a coluna fkProperty apontando pro id do pai.
                var oneToOne = TypeMetadataCache.GetOneToOneAttribute(prop);
                if (oneToOne != null && oneToOne.Cascade)
                {
                    await SaveOneToOneCascadeAsync(connection, dialect, tx, root, prop, oneToOne, idProp);
                }
            }
        }

        private static async Task SaveOneToManyAsync(
            DbConnection connection, IDialect dialect, DbTransaction tx,
            object root, PropertyInfo navProp, OneToManyAttribute attr, PropertyInfo parentIdProp)
        {
            var children = navProp.GetValue(root) as IEnumerable;
            if (children == null) return;

            var childFkProp = ResolveColumnProperty(attr.RelatedType, attr.ForeignKeyProperty);
            if (childFkProp == null)
                throw new InvalidOperationException(
                    $"Cascade OneToMany: tipo {attr.RelatedType.Name} não tem propriedade/coluna '{attr.ForeignKeyProperty}'.");

            var parentId = parentIdProp.GetValue(root);
            foreach (var child in children)
            {
                if (child == null) continue;
                AssignFk(childFkProp, child, parentId);
                await SaveDynamicAsync(connection, dialect, tx, child, attr.RelatedType);
            }
        }

        private static async Task SaveOneToOneCascadeAsync(
            DbConnection connection, IDialect dialect, DbTransaction tx,
            object root, PropertyInfo navProp, OneToOneAttribute attr, PropertyInfo parentIdProp)
        {
            var child = navProp.GetValue(root);
            if (child == null) return;

            var childFkProp = ResolveColumnProperty(attr.RelatedType, attr.ForeignKeyProperty);
            if (childFkProp == null)
                throw new InvalidOperationException(
                    $"Cascade OneToOne: tipo {attr.RelatedType.Name} não tem propriedade/coluna '{attr.ForeignKeyProperty}'.");

            AssignFk(childFkProp, child, parentIdProp.GetValue(root));
            await SaveDynamicAsync(connection, dialect, tx, child, attr.RelatedType);
        }

        private static void AssignFk(PropertyInfo childFkProp, object child, object parentId)
        {
            if (parentId == null) return;
            var target = Nullable.GetUnderlyingType(childFkProp.PropertyType) ?? childFkProp.PropertyType;
            object value = parentId.GetType() == target
                ? parentId
                : Convert.ChangeType(parentId, target, System.Globalization.CultureInfo.InvariantCulture);
            childFkProp.SetValue(child, value);
        }

        private static PropertyInfo ResolveColumnProperty(Type type, string nameOrColumn)
        {
            foreach (var p in TypeMetadataCache.GetProperties(type))
            {
                if (p.Name == nameOrColumn) return p;
                var col = TypeMetadataCache.GetColumnAttribute(p);
                if (col != null && col.Name == nameOrColumn) return p;
            }
            return null;
        }

        // Constrói SqlRepository<TChild,TChildId> em runtime e chama SaveAsync.
        // TChildId é descoberto pelo segundo argumento genérico de BaseModel<,>
        // na hierarquia do filho.
        private static async Task SaveDynamicAsync(
            DbConnection connection, IDialect dialect, DbTransaction tx,
            object child, Type childType)
        {
            var (modelType, idType) = ResolveBaseModelArgs(childType);
            var repoType = typeof(SqlRepository<,>).MakeGenericType(modelType, idType);
            var repo = Activator.CreateInstance(repoType, connection, dialect, tx);
            var saveMethod = repoType.GetMethod("SaveAsync", new[] { modelType });
            var task = (Task)saveMethod.Invoke(repo, new[] { child });
            await task.ConfigureAwait(false);
        }

        private static (Type modelType, Type idType) ResolveBaseModelArgs(Type childType)
        {
            var t = childType;
            while (t != null && t != typeof(object))
            {
                if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(BaseModel<,>))
                {
                    var args = t.GetGenericArguments();
                    return (args[0], args[1]);
                }
                t = t.BaseType;
            }
            throw new InvalidOperationException(
                $"Cascade requer que {childType.Name} herde BaseModel<T, TId>.");
        }
    }
}
