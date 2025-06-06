using LightOrm.Core.Models;
using LightOrm.Core.Attributes;
using System;

namespace LightOrm.Core.Tests.Models
{
    public class TestUserModel : BaseModel<TestUserModel>
    {
        public override string TableName => "test_users";

        [Column("user_name", length: 100)]
        public string UserName { get; set; }

        [Column("email_address", length: 255)]
        public string EmailAddress { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; }
    }

    public class TestPostModel : BaseModel<TestPostModel>
    {
        public override string TableName => "test_posts";

        [Column("post_title", length: 200)]
        public string PostTitle { get; set; }

        [Column("post_content", length: 1000)]
        public string PostContent { get; set; }

        [Column("user_id")]
        [ForeignKey("test_users")]
        public int UserId { get; set; }

        [OneToOne("UserId", typeof(TestUserModel))]
        public TestUserModel User { get; set; }
    }

    public class MaliciousTableNameModel : BaseModel<MaliciousTableNameModel>
    {
        public override string TableName => "`malicious_table`; DROP TABLE users; --";

        [Column("data")]
        public string Data { get; set; }
    }

    public class MaliciousColumnNameModel : BaseModel<MaliciousColumnNameModel>
    {
        public override string TableName => "safe_table";

        [Column("`malicious_column`; DROP TABLE users; --`")]
        public string MaliciousColumn { get; set; }
    }
}

