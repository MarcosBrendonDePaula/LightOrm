using System;
using LightOrm.Core.Models;
using LightOrm.Core.Attributes;

namespace LightOrm.Tests.Models
{
    public class UserModel : BaseModel<UserModel>
    {
        public override string TableName => "users";

        [Column("name", length: 100)]
        public string Name { get; set; }

        [Column("email", length: 255)]
        public string Email { get; set; }

        [Column("age", isUnsigned: true)]
        public int Age { get; set; }

        [Column("balance", isUnsigned: true)]
        public decimal Balance { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; }

        // Example of a one-to-many relationship
        [OneToMany("user_id", typeof(PostModel))]
        public PostModel[] Posts { get; set; }
    }

    public class PostModel : BaseModel<PostModel>
    {
        public override string TableName => "posts";

        [Column("title", length: 200)]
        public string Title { get; set; }

        [Column("content", length: 1000)]
        public string Content { get; set; }

        [Column("likes", isUnsigned: true)]
        public int Likes { get; set; }

        [Column("user_id")]
        [ForeignKey("users")]
        public int UserId { get; set; }

        // Example of a one-to-one relationship with User
        [OneToOne("UserId", typeof(UserModel))]
        public UserModel User { get; set; }
    }
}
