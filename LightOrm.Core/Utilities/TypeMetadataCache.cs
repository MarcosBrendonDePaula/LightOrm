using System;
using System.Collections.Concurrent;
using System.Reflection;
using LightOrm.Core.Attributes;

namespace LightOrm.Core.Utilities
{
    public static class TypeMetadataCache
    {
        private static readonly ConcurrentDictionary<Type, PropertyInfo[]> _propertiesCache = new ConcurrentDictionary<Type, PropertyInfo[]>();
        private static readonly ConcurrentDictionary<PropertyInfo, ColumnAttribute> _columnAttributeCache = new ConcurrentDictionary<PropertyInfo, ColumnAttribute>();
        private static readonly ConcurrentDictionary<PropertyInfo, ForeignKeyAttribute> _foreignKeyAttributeCache = new ConcurrentDictionary<PropertyInfo, ForeignKeyAttribute>();
        private static readonly ConcurrentDictionary<PropertyInfo, OneToManyAttribute> _oneToManyAttributeCache = new ConcurrentDictionary<PropertyInfo, OneToManyAttribute>();
        private static readonly ConcurrentDictionary<PropertyInfo, OneToOneAttribute> _oneToOneAttributeCache = new ConcurrentDictionary<PropertyInfo, OneToOneAttribute>();
        private static readonly ConcurrentDictionary<PropertyInfo, ManyToManyAttribute> _manyToManyAttributeCache = new ConcurrentDictionary<PropertyInfo, ManyToManyAttribute>();

        public static PropertyInfo[] GetProperties(Type type)
        {
            return _propertiesCache.GetOrAdd(type, t => t.GetProperties());
        }

        public static ColumnAttribute GetColumnAttribute(PropertyInfo property)
        {
            return _columnAttributeCache.GetOrAdd(property, p => p.GetCustomAttribute<ColumnAttribute>());
        }

        public static ForeignKeyAttribute GetForeignKeyAttribute(PropertyInfo property)
        {
            return _foreignKeyAttributeCache.GetOrAdd(property, p => p.GetCustomAttribute<ForeignKeyAttribute>());
        }

        public static OneToManyAttribute GetOneToManyAttribute(PropertyInfo property)
        {
            return _oneToManyAttributeCache.GetOrAdd(property, p => p.GetCustomAttribute<OneToManyAttribute>());
        }

        public static OneToOneAttribute GetOneToOneAttribute(PropertyInfo property)
        {
            return _oneToOneAttributeCache.GetOrAdd(property, p => p.GetCustomAttribute<OneToOneAttribute>());
        }

        public static ManyToManyAttribute GetManyToManyAttribute(PropertyInfo property)
        {
            return _manyToManyAttributeCache.GetOrAdd(property, p => p.GetCustomAttribute<ManyToManyAttribute>());
        }
    }
}


