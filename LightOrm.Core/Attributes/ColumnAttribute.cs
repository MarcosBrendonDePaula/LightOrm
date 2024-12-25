using System;

namespace LightOrm.Core.Attributes
{
    /// <summary>
    /// Attribute to define a column in a database model.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class ColumnAttribute : Attribute
    {
        public string Name { get; }
        public bool IsPrimaryKey { get; }
        public bool AutoIncrement { get; }
        public int Length { get; }
        public string[] EnumValues { get; }
        public bool IsUnsigned { get; }

        public ColumnAttribute(string name, bool isPrimaryKey = false, bool autoIncrement = false, int length = -1, bool isUnsigned = false, params string[] enumValues)
        {
            Name = name;
            IsPrimaryKey = isPrimaryKey;
            AutoIncrement = autoIncrement;
            Length = length;
            EnumValues = enumValues;
            IsUnsigned = isUnsigned;
        }
    }
}
