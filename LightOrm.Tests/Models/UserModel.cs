using System;
using LightOrm.Core.Models;
using LightOrm.Core.Attributes;

namespace LightOrm.Tests.Models
{
    public class UserModel : BaseModel<UserModel>
    {
        public override string TableName => "users";

        [Column("name", length: 100, isNullable: false)]
        public string Name { get; set; }

        [Column("email", length: 255, isNullable: false)]
        public string Email { get; set; }

        [Column("age", isUnsigned: true, isNullable: true)]
        public int? Age { get; set; }

        [Column("balance", isUnsigned: true, isNullable: false, defaultValue: 0.0)]
        public decimal Balance { get; set; }

        [Column("is_active", isNullable: false, defaultValue: true)]
        public bool IsActive { get; set; }

        [Column("last_login", isNullable: true)]
        public DateTime? LastLogin { get; set; }

        [Column("notes", length: 500, isNullable: true, defaultValue: "")]
        public string Notes { get; set; }

        // Example of a one-to-many relationship
        [OneToMany("user_id", typeof(PostModel))]
        public PostModel[] Posts { get; set; }
    }

    public class PostModel : BaseModel<PostModel>
    {
        public override string TableName => "posts";

        [Column("title", length: 200, isNullable: false)]
        public string Title { get; set; }

        [Column("content", length: 1000, isNullable: true)]
        public string Content { get; set; }

        [Column("likes", isUnsigned: true, isNullable: false, defaultValue: 0)]
        public int Likes { get; set; }

        [Column("published_at", isNullable: true)]
        public DateTime? PublishedAt { get; set; }

        [Column("status", length: 20, isNullable: false, defaultValue: "draft")]
        public string Status { get; set; }

        [Column("user_id", isNullable: false)]
        [ForeignKey("users")]
        public int UserId { get; set; }

        // Example of a one-to-one relationship with User
        [OneToOne("UserId", typeof(UserModel))]
        public UserModel User { get; set; }
    }
}
