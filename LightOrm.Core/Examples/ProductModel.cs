using System;
using LightOrm.Core.Models;
using LightOrm.Core.Attributes;

namespace LightOrm.Core.Examples
{
    public class ProductModel : BaseModel<ProductModel>
    {
        public override string TableName => "products";

        // Required fields with validation
        [Column("sku", length: 50, isNullable: false)]
        public string SKU { get; set; }

        [Column("name", length: 200, isNullable: false)]
        public string Name { get; set; }

        // Optional fields with defaults
        [Column("description", length: 1000, isNullable: true, defaultValue: "")]
        public string Description { get; set; }

        [Column("price", isNullable: false, defaultValue: 0.0)]
        public decimal Price { get; set; }

        [Column("stock_quantity", isUnsigned: true, isNullable: false, defaultValue: 0)]
        public int StockQuantity { get; set; }

        // Status management
        [Column("status", length: 20, isNullable: false, defaultValue: "draft")]
        public string Status { get; set; }

        [Column("is_featured", isNullable: false, defaultValue: false)]
        public bool IsFeatured { get; set; }

        // Dates that can be null
        [Column("published_at", isNullable: true)]
        public DateTime? PublishedAt { get; set; }

        [Column("last_ordered_at", isNullable: true)]
        public DateTime? LastOrderedAt { get; set; }

        // Optional metadata
        [Column("weight_kg", isNullable: true)]
        public decimal? WeightKg { get; set; }

        [Column("dimensions", length: 50, isNullable: true)]
        public string Dimensions { get; set; }

        [Column("category_id", isNullable: true)]
        [ForeignKey("categories", "Id")]
        public int? CategoryId { get; set; }
    }

    public class CategoryModel : BaseModel<CategoryModel>
    {
        public override string TableName => "categories";

        [Column("name", length: 100, isNullable: false)]
        public string Name { get; set; }

        [Column("slug", length: 100, isNullable: false)]
        public string Slug { get; set; }

        [Column("description", length: 500, isNullable: true, defaultValue: "")]
        public string Description { get; set; }

        [Column("parent_id", isNullable: true)]
        [ForeignKey("categories", "Id")]
        public int? ParentId { get; set; }

        [Column("display_order", isNullable: false, defaultValue: 0)]
        public int DisplayOrder { get; set; }

        [Column("is_active", isNullable: false, defaultValue: true)]
        public bool IsActive { get; set; }
    }
}
