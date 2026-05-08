using System;
using System.Data.Common;
using System.Reflection;
using System.Threading.Tasks;
using LightOrm.Core.Attributes;
using LightOrm.Core.Models;
using LightOrm.Core.Utilities;

namespace LightOrm.Core.Sql
{
    /// <summary>
    /// Antes do DELETE do pai, apaga filhos OneToMany/OneToOne com cascadeDelete.
    /// Em soft-delete vira UPDATE deleted_at (consistente com o resto).
    /// </summary>
    internal static class CascadeDeleter
    {
        public static async Task DeleteCascadesAsync(
            DbConnection connection, IDialect dialect, DbTransaction tx,
            Type rootType, object root, PropertyInfo idProp)
        {
            var parentId = idProp.GetValue(root);
            foreach (var prop in TypeMetadataCache.GetProperties(rootType))
            {
                var oneToMany = TypeMetadataCache.GetOneToManyAttribute(prop);
                if (oneToMany != null && oneToMany.CascadeDelete)
                {
                    await BulkDeleteByFkAsync(connection, dialect, tx,
                        oneToMany.RelatedType, oneToMany.ForeignKeyProperty, parentId);
                    continue;
                }
                var oneToOne = TypeMetadataCache.GetOneToOneAttribute(prop);
                if (oneToOne != null && oneToOne.CascadeDelete)
                {
                    await BulkDeleteByFkAsync(connection, dialect, tx,
                        oneToOne.RelatedType, oneToOne.ForeignKeyProperty, parentId);
                }
            }
        }

        private static async Task BulkDeleteByFkAsync(
            DbConnection connection, IDialect dialect, DbTransaction tx,
            Type childType, string fkPropertyOrColumn, object parentId)
        {
            // Resolve nome da coluna FK no filho.
            string fkColumn = fkPropertyOrColumn;
            foreach (var p in TypeMetadataCache.GetProperties(childType))
            {
                if (p.Name == fkPropertyOrColumn)
                {
                    var col = TypeMetadataCache.GetColumnAttribute(p);
                    if (col != null) { fkColumn = col.Name; break; }
                }
                var c2 = TypeMetadataCache.GetColumnAttribute(p);
                if (c2 != null && c2.Name == fkPropertyOrColumn) { fkColumn = c2.Name; break; }
            }

            // Tabela do filho.
            var childInstance = (IModel)Activator.CreateInstance(childType);
            var childTable = childInstance.GetTableName();

            // Soft-delete? Faz UPDATE em vez de DELETE.
            var sdAttr = TypeMetadataCache.GetSoftDeleteAttribute(childType);
            string sql;
            DbCommand cmd;
            if (sdAttr != null)
            {
                sql = $"UPDATE {dialect.QuoteIdentifier(childTable)} SET " +
                      $"{dialect.QuoteIdentifier(sdAttr.ColumnName)} = {dialect.ParameterPrefix}__sdNow " +
                      $"WHERE {dialect.QuoteIdentifier(fkColumn)} = {dialect.ParameterPrefix}__pid";
                cmd = dialect.CreateCommand(connection);
                cmd.CommandText = sql;
                cmd.Transaction = tx;

                var pNow = cmd.CreateParameter();
                pNow.ParameterName = dialect.ParameterPrefix + "__sdNow";
                pNow.Value = dialect.ToDbValue(DateTime.UtcNow, typeof(DateTime?)) ?? DBNull.Value;
                cmd.Parameters.Add(pNow);
            }
            else
            {
                sql = $"DELETE FROM {dialect.QuoteIdentifier(childTable)} " +
                      $"WHERE {dialect.QuoteIdentifier(fkColumn)} = {dialect.ParameterPrefix}__pid";
                cmd = dialect.CreateCommand(connection);
                cmd.CommandText = sql;
                cmd.Transaction = tx;
            }

            try
            {
                var pPid = cmd.CreateParameter();
                pPid.ParameterName = dialect.ParameterPrefix + "__pid";
                pPid.Value = parentId == null ? DBNull.Value
                    : (dialect.ToDbValue(parentId, parentId.GetType()) ?? DBNull.Value);
                cmd.Parameters.Add(pPid);
                await cmd.ExecuteNonQueryAsync();
            }
            finally
            {
                cmd.Dispose();
            }
        }
    }
}
