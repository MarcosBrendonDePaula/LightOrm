using System;
using System.Reflection;
using LightOrm.Core.Attributes;
using LightOrm.Core.Utilities;

namespace LightOrm.Core.Sql
{
    internal static class SoftDeleteHelper
    {
        // Devolve (column-name, property) quando o tipo tem [SoftDelete]; senão (null, null).
        // A propriedade é sempre BaseModel.DeletedAt (DateTime?).
        public static (string columnName, PropertyInfo prop) Resolve(Type type)
        {
            var attr = TypeMetadataCache.GetSoftDeleteAttribute(type);
            if (attr == null) return (null, null);
            var prop = type.GetProperty("DeletedAt");
            if (prop == null)
                throw new InvalidOperationException(
                    $"[SoftDelete] em {type.Name}: propriedade DeletedAt não encontrada (verifique a herança de BaseModel).");
            return (attr.ColumnName, prop);
        }
    }
}
