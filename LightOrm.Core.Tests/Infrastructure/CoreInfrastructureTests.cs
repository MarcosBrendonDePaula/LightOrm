using System;
using System.Linq;
using LightOrm.Core.Attributes;
using LightOrm.Core.Models;
using LightOrm.Core.Sql;
using LightOrm.Core.Utilities;
using LightOrm.Core.Validation;

namespace LightOrm.Core.Tests
{
    public class CoreInfrastructureTests
    {
        [Fact]
        public void RepositoryFactory_Create_throws_for_null_factory()
        {
            Assert.Throws<ArgumentNullException>(() => LightOrm.Core.RepositoryFactory.Create<FactoryModel, int>(null));
        }

        [Fact]
        public void RepositoryFactory_Create_returns_factory_result()
        {
            var expected = new FakeRepository();

            var repo = LightOrm.Core.RepositoryFactory.Create<FactoryModel, int>(() => expected);

            Assert.Same(expected, repo);
        }

        [Fact]
        public void BaseModel_TableName_uses_override_when_present_even_with_Table_attribute()
        {
            var model = new TableWinsOverOverrideModel();

            Assert.Equal("override_should_lose", model.TableName);
        }

        [Fact]
        public void TypeMetadataCache_reads_inherited_class_attributes_and_caches_results()
        {
            var properties1 = TypeMetadataCache.GetProperties(typeof(InheritedMetadataModel));
            var properties2 = TypeMetadataCache.GetProperties(typeof(InheritedMetadataModel));
            var nameProp = properties1.Single(p => p.Name == nameof(InheritedMetadataModel.Name));
            var column1 = TypeMetadataCache.GetColumnAttribute(nameProp);
            var column2 = TypeMetadataCache.GetColumnAttribute(nameProp);
            var table1 = TypeMetadataCache.GetTableAttribute(typeof(InheritedMetadataModel));
            var table2 = TypeMetadataCache.GetTableAttribute(typeof(InheritedMetadataModel));
            var softDelete1 = TypeMetadataCache.GetSoftDeleteAttribute(typeof(InheritedMetadataModel));
            var softDelete2 = TypeMetadataCache.GetSoftDeleteAttribute(typeof(InheritedMetadataModel));

            Assert.Same(properties1, properties2);
            Assert.Same(column1, column2);
            Assert.Same(table1, table2);
            Assert.Same(softDelete1, softDelete2);
            Assert.Equal("inherited_table", table1.Name);
            Assert.Equal("child_name", column1.Name);
            Assert.Equal("archived_at", softDelete1.ColumnName);
        }

        [Fact]
        public void SoftDeleteHelper_Resolve_uses_inherited_attribute_and_deleted_at_property()
        {
            var (columnName, prop) = SoftDeleteHelper.Resolve(typeof(InheritedMetadataModel));

            Assert.Equal("archived_at", columnName);
            Assert.NotNull(prop);
            Assert.Equal(nameof(BaseModel<InheritedMetadataModel, int>.DeletedAt), prop.Name);
            Assert.Equal(typeof(DateTime?), prop.PropertyType);
        }

        [Fact]
        public void ModelValidator_Validate_throws_for_null_entity()
        {
            Assert.Throws<ArgumentNullException>(() => ModelValidator.Validate(null));
        }

        [Fact]
        public void ModelValidator_aggregates_multiple_errors_from_the_same_entity()
        {
            var entity = new MultiErrorValidatedModel
            {
                Code = "ab",
                Score = 99
            };

            var ex = Assert.Throws<ValidationException>(() => ModelValidator.Validate(entity));

            Assert.Equal(3, ex.Errors.Count);
            Assert.Contains(ex.Errors, e => e.PropertyName == nameof(MultiErrorValidatedModel.Code) && e.Message.Contains("mínimo"));
            Assert.Contains(ex.Errors, e => e.PropertyName == nameof(MultiErrorValidatedModel.Code) && e.Message.Contains("não casa com padrão"));
            Assert.Contains(ex.Errors, e => e.PropertyName == nameof(MultiErrorValidatedModel.Score) && e.Message.Contains("fora do intervalo"));
        }

        [Fact]
        public void ModelValidator_accepts_valid_entity()
        {
            var entity = new MultiErrorValidatedModel
            {
                Code = "ABC",
                Score = 5
            };

            ModelValidator.Validate(entity);
        }

        [Table("table_attr_wins")]
        private sealed class TableWinsOverOverrideModel : BaseModel<TableWinsOverOverrideModel, int>
        {
            public override string TableName => "override_should_lose";
        }

        [Table("inherited_table")]
        [SoftDelete("archived_at")]
        private abstract class InheritedMetadataBase : BaseModel<InheritedMetadataModel, int>
        {
            public override string TableName => "override_should_lose";
        }

        private sealed class InheritedMetadataModel : InheritedMetadataBase
        {
            [Column("child_name", length: 64)]
            public string Name { get; set; }
        }

        private sealed class MultiErrorValidatedModel
        {
            [MinLength(3)]
            [RegEx("^[A-Z]+$")]
            public string Code { get; set; }

            [Range(0, 10)]
            public int Score { get; set; }
        }
    }

    internal sealed class FactoryModel : BaseModel<FactoryModel, int>
    {
        public override string TableName => "factory_model";
    }

    internal sealed class FakeRepository : IRepository<FactoryModel, int>
    {
        public System.Threading.Tasks.Task EnsureSchemaAsync() => throw new NotSupportedException();
        public System.Threading.Tasks.Task<FactoryModel> SaveAsync(FactoryModel entity) => throw new NotSupportedException();
        public System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<FactoryModel>> SaveManyAsync(System.Collections.Generic.IEnumerable<FactoryModel> entities) => throw new NotSupportedException();
        public System.Threading.Tasks.Task DeleteAsync(FactoryModel entity) => throw new NotSupportedException();
        public System.Threading.Tasks.Task<FactoryModel> FindByIdAsync(int id, bool includeRelated = false) => throw new NotSupportedException();
        public System.Threading.Tasks.Task<System.Collections.Generic.List<FactoryModel>> FindAllAsync(bool includeRelated = false) => throw new NotSupportedException();
        public IQuery<FactoryModel, int> Query() => throw new NotSupportedException();
        public System.Threading.Tasks.Task<FactoryModel> UpsertAsync(FactoryModel entity) => throw new NotSupportedException();
        public System.Threading.Tasks.Task<(FactoryModel entity, bool created)> FindOrCreateAsync(Action<IQuery<FactoryModel, int>> filter, FactoryModel defaults) => throw new NotSupportedException();
    }
}
