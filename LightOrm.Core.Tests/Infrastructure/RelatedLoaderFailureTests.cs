using System;
using System.Threading.Tasks;
using LightOrm.Core.Attributes;
using LightOrm.Core.Models;
using LightOrm.Core.Sql;
using LightOrm.Sqlite;
using Microsoft.Data.Sqlite;
using Xunit;

namespace LightOrm.Core.Tests
{
    public class RelatedLoaderFailureTests
    {
        private static (SqliteConnection conn, IDialect dialect) Open()
        {
            var c = new SqliteConnection("Data Source=:memory:");
            c.Open();
            return (c, new SqliteDialect());
        }

        [Fact]
        public async Task IncludeRelated_with_related_type_without_parameterless_constructor_throws_clear_error()
        {
            var (conn, dialect) = Open();
            var roots = new SqlRepository<NoDefaultCtorRootModel, int>(conn, dialect);
            await roots.EnsureSchemaAsync();

            var root = await roots.SaveAsync(new NoDefaultCtorRootModel { Name = "root", RelatedId = 7 });

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => roots.FindByIdAsync(root.Id, includeRelated: true));

            Assert.Contains("construtor sem parâmetros", ex.Message);
            Assert.Contains(nameof(NoDefaultCtorRelatedIModel), ex.Message);
        }
    }

    public class NoDefaultCtorRootModel : BaseModel<NoDefaultCtorRootModel, int>
    {
        public override string TableName => "nodefault_root";

        [Column("name", length: 100)]
        public string? Name { get; set; }

        [Column("related_id")]
        public int RelatedId { get; set; }

        [OneToOne("RelatedId", typeof(NoDefaultCtorRelatedIModel))]
        public NoDefaultCtorRelatedIModel? Related { get; set; }
    }

    public class NoDefaultCtorRelatedIModel : IModel
    {
        public NoDefaultCtorRelatedIModel(string code)
        {
            Code = code;
        }

        public string Code { get; set; }

        public string GetTableName() => "nodefault_related";
    }
}
