using System;

namespace LightOrm.Core.Attributes
{
    /// <summary>
    /// Attribute to define a foreign key relationship in a database model.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class ForeignKeyAttribute : Attribute
    {
        public string ReferenceTable { get; }
        public string ReferenceColumn { get; }

        public ForeignKeyAttribute(string referenceTable, string referenceColumn = "Id")
        {
            ReferenceTable = referenceTable;
            ReferenceColumn = referenceColumn;
        }
    }
}
