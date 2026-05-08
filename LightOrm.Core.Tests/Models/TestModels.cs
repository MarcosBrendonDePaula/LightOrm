using LightOrm.Core.Models;
using LightOrm.Core.Attributes;

namespace LightOrm.Core.Tests.Models
{
    public class TestUserModel : BaseModel<TestUserModel, int>
    {
        public override string TableName => "test_users";

        [Column("user_name", length: 100)]
        public string UserName { get; set; }

        [Column("email_address", length: 255)]
        public string EmailAddress { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; }
    }

    public class TestUserMongoModel : BaseModel<TestUserMongoModel, string>
    {
        public override string TableName => "test_users";

        [Column("user_name", length: 100)]
        public string UserName { get; set; }

        [Column("email_address", length: 255)]
        public string EmailAddress { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; }
    }

    public class MaliciousTableNameModel : BaseModel<MaliciousTableNameModel, int>
    {
        public override string TableName => "`malicious_table`; DROP TABLE users; --";

        [Column("data")]
        public string Data { get; set; }
    }

    public class MaliciousColumnNameModel : BaseModel<MaliciousColumnNameModel, int>
    {
        public override string TableName => "safe_table";

        [Column("`malicious_column`; DROP TABLE users; --`")]
        public string MaliciousColumn { get; set; }
    }
}
