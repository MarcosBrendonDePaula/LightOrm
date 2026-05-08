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

    // Subdocumento embedded — não herda BaseModel, sem id próprio.
    public class EmbeddedAddress
    {
        [Column("street")]
        public string Street { get; set; }

        [Column("city")]
        public string City { get; set; }

        [Column("zip")]
        public string Zip { get; set; }
    }

    [SoftDelete]
    public class SoftDeletedMongoModel : BaseModel<SoftDeletedMongoModel, string>
    {
        public override string TableName => "soft_deleted_mongo";

        [Column("name")]
        public string Name { get; set; }
    }

    public class VersionedMongoModel : BaseModel<VersionedMongoModel, string>
    {
        public override string TableName => "versioned_mongo";

        [Column("name")]
        public string Name { get; set; }

        [Column("row_version")]
        [Version]
        public int RowVersion { get; set; }
    }

    public class TestUserWithEmbedsModel : BaseModel<TestUserWithEmbedsModel, string>
    {
        public override string TableName => "users_with_embeds";

        [Column("name")]
        public string Name { get; set; }

        // 1:N embed — array de subdocumentos.
        [Embedded]
        public EmbeddedAddress[] Addresses { get; set; }

        // 1:1 embed — único subdocumento.
        [Embedded]
        public EmbeddedAddress PrimaryAddress { get; set; }
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
