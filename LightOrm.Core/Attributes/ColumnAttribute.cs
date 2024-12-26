using System;

namespace LightOrm.Core.Attributes
{
    /// <summary>
    /// Attribute to define a column in a database model.
    /// </summary>
    /// <remarks>
    /// Special handling for timestamps:
    /// - CreatedAt: Automatically set to CURRENT_TIMESTAMP when record is created
    /// - UpdatedAt: Automatically updated to CURRENT_TIMESTAMP when record is modified
    /// 
    /// Example usages:
    /// 
    /// Basic column:
    /// [Column("name", length: 100, isNullable: false)]
    /// 
    /// Unique column with index:
    /// [Column("email", length: 255, isNullable: false, isUnique: true, hasIndex: true)]
    /// 
    /// Enum-like column:
    /// [Column("status", enumValues: new[] { "active", "inactive", "pending" })]
    /// 
    /// Decimal with precision:
    /// [Column("price", precision: 10, scale: 2)]
    /// 
    /// Integer with check constraint:
    /// [Column("age", checkConstraint: "age >= 0 AND age <= 150")]
    /// 
    /// Auto-timestamp:
    /// [Column("created_at", defaultValue: "CURRENT_TIMESTAMP")]
    /// [Column("updated_at", defaultValue: "CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP")]
    /// </remarks>
    [AttributeUsage(AttributeTargets.Property)]
    public class ColumnAttribute : Attribute
    {
        /// <summary>
        /// The name of the column in the database table.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Whether this column is the primary key of the table.
        /// </summary>
        public bool IsPrimaryKey { get; }

        /// <summary>
        /// Whether this column should auto-increment (typically used with primary keys).
        /// </summary>
        public bool AutoIncrement { get; }

        /// <summary>
        /// The maximum length for string columns. Default is 255 if not specified.
        /// </summary>
        public int Length { get; }

        /// <summary>
        /// For enum-like columns, specifies the allowed values. Creates a CHECK constraint.
        /// </summary>
        public string[] EnumValues { get; }

        /// <summary>
        /// For numeric columns, specifies if the column should be unsigned.
        /// </summary>
        public bool IsUnsigned { get; }

        /// <summary>
        /// Whether the column can contain NULL values.
        /// </summary>
        public bool IsNullable { get; }

        /// <summary>
        /// The default value for the column. Special values:
        /// - "CURRENT_TIMESTAMP" for automatic timestamps
        /// - "CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP" for auto-updating timestamps
        /// </summary>
        public object DefaultValue { get; }

        /// <summary>
        /// Whether the column should have a unique constraint.
        /// </summary>
        public bool IsUnique { get; }

        /// <summary>
        /// Whether to create an index on this column.
        /// </summary>
        public bool HasIndex { get; }

        /// <summary>
        /// Custom CHECK constraint for the column (e.g., "age >= 0 AND age <= 150").
        /// </summary>
        public string CheckConstraint { get; }

        /// <summary>
        /// For decimal columns, the total number of digits.
        /// </summary>
        public int Precision { get; }

        /// <summary>
        /// For decimal columns, the number of digits after the decimal point.
        /// </summary>
        public int Scale { get; }

        /// <summary>
        /// Creates a new column definition with the specified configuration.
        /// </summary>
        /// <param name="name">The name of the column in the database table</param>
        /// <param name="isPrimaryKey">Whether this column is the primary key</param>
        /// <param name="autoIncrement">Whether this column should auto-increment</param>
        /// <param name="length">The maximum length for string columns</param>
        /// <param name="isUnsigned">For numeric columns, whether the column should be unsigned</param>
        /// <param name="isNullable">Whether the column can contain NULL values</param>
        /// <param name="defaultValue">The default value for the column</param>
        /// <param name="isUnique">Whether the column should have a unique constraint</param>
        /// <param name="hasIndex">Whether to create an index on this column</param>
        /// <param name="checkConstraint">Custom CHECK constraint for the column</param>
        /// <param name="precision">For decimal columns, the total number of digits</param>
        /// <param name="scale">For decimal columns, the number of digits after the decimal point</param>
        /// <param name="enumValues">For enum-like columns, the allowed values</param>
        public ColumnAttribute(
            string name, 
            bool isPrimaryKey = false, 
            bool autoIncrement = false, 
            int length = -1, 
            bool isUnsigned = false,
            bool isNullable = false,
            object defaultValue = null,
            bool isUnique = false,
            bool hasIndex = false,
            string checkConstraint = null,
            int precision = 18,
            int scale = 2,
            params string[] enumValues)
        {
            Name = name;
            IsPrimaryKey = isPrimaryKey;
            AutoIncrement = autoIncrement;
            Length = length;
            EnumValues = enumValues;
            IsUnsigned = isUnsigned;
            IsNullable = isNullable;
            DefaultValue = defaultValue;
            IsUnique = isUnique;
            HasIndex = hasIndex;
            CheckConstraint = checkConstraint;
            Precision = precision;
            Scale = scale;
        }
    }
}
