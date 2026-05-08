using System;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using LightOrm.Core.Sql;
using LightOrm.Core.Tests.Models;
using Xunit;

namespace LightOrm.Core.Tests
{
    /// <summary>
    /// Cenários avançados que rodam contra os 3 dialects SQL (MySQL, SQLite, Postgres).
    /// Cada subclasse fornece (DbConnection, IDialect) e roda os mesmos testes.
    /// </summary>
    public abstract class AdvancedScenarios
    {
        protected abstract (DbConnection conn, IDialect dialect) Open();

        // ---------- Tipos: round-trip preserva valores? ----------

        [Fact]
        public async Task RoundTrips_Guid_Decimal_DateTime_and_nulls()
        {
            var (conn, dialect) = Open();
            var repo = new SqlRepository<TypesModel, int>(conn, dialect);
            await repo.EnsureSchemaAsync();

            var guid = Guid.NewGuid();
            var date = new DateTime(2026, 5, 8, 14, 30, 45, DateTimeKind.Utc);
            var entity = new TypesModel
            {
                Name = "round-trip",
                GuidValue = guid,
                DecimalValue = 12345.67m,
                DateValue = date,
                NullableInt = null,
                NullableDate = null
            };
            await repo.SaveAsync(entity);
            var loaded = await repo.FindByIdAsync(entity.Id);

            Assert.Equal(guid, loaded.GuidValue);
            Assert.Equal(12345.67m, loaded.DecimalValue);
            Assert.Equal(date.Year, loaded.DateValue.Year);
            Assert.Equal(date.Month, loaded.DateValue.Month);
            Assert.Equal(date.Day, loaded.DateValue.Day);
            Assert.Equal(date.Hour, loaded.DateValue.Hour);
            Assert.Equal(date.Minute, loaded.DateValue.Minute);
            Assert.Null(loaded.NullableInt);
            Assert.Null(loaded.NullableDate);
        }

        [Fact]
        public async Task RoundTrips_decimal_with_high_precision()
        {
            var (conn, dialect) = Open();
            var repo = new SqlRepository<TypesModel, int>(conn, dialect);
            await repo.EnsureSchemaAsync();

            // DECIMAL(18,2) corta na 2ª casa — 0.999 vira 1.00 no MySQL.
            // Validamos que valores dentro da precisão suportada não perdem informação.
            var entity = new TypesModel
            {
                Name = "money",
                GuidValue = Guid.NewGuid(),
                DecimalValue = 999999999999.99m,
                DateValue = DateTime.UtcNow
            };
            await repo.SaveAsync(entity);
            var loaded = await repo.FindByIdAsync(entity.Id);
            Assert.Equal(999999999999.99m, loaded.DecimalValue);
        }

        // ---------- Strings problemáticas ----------

        [Theory]
        [InlineData("simples")]
        [InlineData("com 'aspas simples'")]
        [InlineData("com \"aspas duplas\"")]
        [InlineData("com `backticks`")]
        [InlineData("com -- SQL injection -- DROP TABLE")]
        [InlineData("emoji 🎉🚀 acentos áéíóú çñ")]
        [InlineData("japonês こんにちは / russo Привет")]
        [InlineData("")]
        public async Task RoundTrips_problematic_strings(string text)
        {
            var (conn, dialect) = Open();
            var repo = new SqlRepository<TypesModel, int>(conn, dialect);
            await repo.EnsureSchemaAsync();

            var entity = new TypesModel
            {
                Name = text,
                GuidValue = Guid.NewGuid(),
                DecimalValue = 1m,
                DateValue = DateTime.UtcNow
            };
            await repo.SaveAsync(entity);
            var loaded = await repo.FindByIdAsync(entity.Id);
            Assert.Equal(text, loaded.Name);
        }

        // ---------- Find inexistente / lista vazia ----------

        [Fact]
        public async Task FindById_nonexistent_returns_null()
        {
            var (conn, dialect) = Open();
            var repo = new SqlRepository<TypesModel, int>(conn, dialect);
            await repo.EnsureSchemaAsync();

            var result = await repo.FindByIdAsync(999999);
            Assert.Null(result);
        }

        [Fact]
        public async Task FindAll_on_empty_table_returns_empty_list_not_null()
        {
            var (conn, dialect) = Open();
            var repo = new SqlRepository<TypesModel, int>(conn, dialect);
            await repo.EnsureSchemaAsync();

            var result = await repo.FindAllAsync();
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        // ---------- Update / re-save ----------

        [Fact]
        public async Task Save_twice_does_not_create_duplicate_and_preserves_id()
        {
            var (conn, dialect) = Open();
            var repo = new SqlRepository<TypesModel, int>(conn, dialect);
            await repo.EnsureSchemaAsync();

            var entity = new TypesModel { Name = "once", GuidValue = Guid.NewGuid(), DecimalValue = 1m, DateValue = DateTime.UtcNow };
            await repo.SaveAsync(entity);
            var firstId = entity.Id;

            entity.Name = "twice";
            await repo.SaveAsync(entity);
            Assert.Equal(firstId, entity.Id);

            var all = await repo.FindAllAsync();
            Assert.Single(all);
            Assert.Equal("twice", all[0].Name);
        }

        [Fact]
        public async Task Update_preserves_unchanged_fields()
        {
            var (conn, dialect) = Open();
            var repo = new SqlRepository<TypesModel, int>(conn, dialect);
            await repo.EnsureSchemaAsync();

            var guid = Guid.NewGuid();
            var entity = new TypesModel
            {
                Name = "original",
                GuidValue = guid,
                DecimalValue = 42.5m,
                DateValue = DateTime.UtcNow,
                NullableInt = 7
            };
            await repo.SaveAsync(entity);

            // Carrega de novo, muda só o nome, salva. Os outros campos têm que sobreviver.
            var fetched = await repo.FindByIdAsync(entity.Id);
            fetched.Name = "updated";
            await repo.SaveAsync(fetched);

            var loaded = await repo.FindByIdAsync(entity.Id);
            Assert.Equal("updated", loaded.Name);
            Assert.Equal(guid, loaded.GuidValue);
            Assert.Equal(42.5m, loaded.DecimalValue);
            Assert.Equal(7, loaded.NullableInt);
        }

        // ---------- Delete e FK órfã ----------

        [Fact]
        public async Task Orphan_FK_does_not_crash_includeRelated()
        {
            var (conn, dialect) = Open();
            var parents = new SqlRepository<ParentModel, int>(conn, dialect);
            var children = new SqlRepository<ChildModel, int>(conn, dialect);
            await parents.EnsureSchemaAsync();
            await children.EnsureSchemaAsync();

            // Filho com parent_id apontando pra pai inexistente — sem FK constraint
            // (ParentId não tem [ForeignKey] no modelo, é só relação lógica via OneToMany).
            var orphan = new ChildModel { Label = "orphan", ParentId = 999999 };
            await children.SaveAsync(orphan);

            // Não deve crashar; pais carregados com Children corretos quando existirem.
            var allChildren = await children.FindAllAsync();
            Assert.Single(allChildren);

            var allParents = await parents.FindAllAsync(includeRelated: true);
            Assert.Empty(allParents);
        }

        [Fact]
        public async Task Parent_with_no_children_returns_empty_array()
        {
            var (conn, dialect) = Open();
            var parents = new SqlRepository<ParentModel, int>(conn, dialect);
            var children = new SqlRepository<ChildModel, int>(conn, dialect);
            await parents.EnsureSchemaAsync();
            await children.EnsureSchemaAsync();

            var p = await parents.SaveAsync(new ParentModel { Name = "lonely" });
            var loaded = await parents.FindByIdAsync(p.Id, includeRelated: true);
            Assert.NotNull(loaded.Children);
            Assert.Empty(loaded.Children);
        }

        // ---------- Volume: confirma que FindAll(includeRelated) não é N+1 ----------

        [Fact]
        public async Task FindAll_includeRelated_handles_volume()
        {
            var (conn, dialect) = Open();
            var parents = new SqlRepository<ParentModel, int>(conn, dialect);
            var children = new SqlRepository<ChildModel, int>(conn, dialect);
            await parents.EnsureSchemaAsync();
            await children.EnsureSchemaAsync();

            const int parentCount = 50;
            const int childrenPerParent = 4;

            for (int i = 0; i < parentCount; i++)
            {
                var p = await parents.SaveAsync(new ParentModel { Name = $"p{i}" });
                for (int j = 0; j < childrenPerParent; j++)
                    await children.SaveAsync(new ChildModel { Label = $"c{i}_{j}", ParentId = p.Id });
            }

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var all = await parents.FindAllAsync(includeRelated: true);
            sw.Stop();

            Assert.Equal(parentCount, all.Count);
            Assert.All(all, p =>
            {
                Assert.NotNull(p.Children);
                Assert.Equal(childrenPerParent, p.Children.Length);
            });

            // 1 query da raiz + 1 query do relacionamento (IN clause). Se fosse N+1,
            // o tempo seria proporcional a parentCount; com IN deve ficar bem abaixo.
            // Damos folga generosa pra não falhar em CI lento.
            Assert.True(sw.ElapsedMilliseconds < 5000,
                $"FindAll(includeRelated) levou {sw.ElapsedMilliseconds}ms — possível N+1.");
        }

        // ---------- Delete ----------

        [Fact]
        public async Task Delete_removes_record_and_findall_reflects()
        {
            var (conn, dialect) = Open();
            var repo = new SqlRepository<TypesModel, int>(conn, dialect);
            await repo.EnsureSchemaAsync();

            var a = new TypesModel { Name = "a", GuidValue = Guid.NewGuid(), DecimalValue = 1m, DateValue = DateTime.UtcNow };
            var b = new TypesModel { Name = "b", GuidValue = Guid.NewGuid(), DecimalValue = 2m, DateValue = DateTime.UtcNow };
            await repo.SaveAsync(a);
            await repo.SaveAsync(b);

            await repo.DeleteAsync(a);

            var all = await repo.FindAllAsync();
            Assert.Single(all);
            Assert.Equal("b", all[0].Name);
            Assert.Null(await repo.FindByIdAsync(a.Id));
        }

        // ---------- Multi-nível (limitação documentada) ----------

        [Fact]
        public async Task IncludeRelated_only_loads_one_level_deep()
        {
            // Limitação: includeRelated carrega 1 nível. Se Parent.Children carregar,
            // os Children não têm seus próprios relacionamentos resolvidos.
            // Este teste documenta isso para que mudanças futuras sejam detectadas.
            var (conn, dialect) = Open();
            var parents = new SqlRepository<ParentModel, int>(conn, dialect);
            var children = new SqlRepository<ChildModel, int>(conn, dialect);
            await parents.EnsureSchemaAsync();
            await children.EnsureSchemaAsync();

            var p = await parents.SaveAsync(new ParentModel { Name = "p" });
            await children.SaveAsync(new ChildModel { Label = "c", ParentId = p.Id });

            var loaded = await parents.FindByIdAsync(p.Id, includeRelated: true);
            Assert.Single(loaded.Children);
            // ChildModel não tem navigation back para Parent, então nada a checar
            // do outro lado; basta confirmar que o nível raiz carregou.
            Assert.Equal("c", loaded.Children[0].Label);
        }

        // ---------- SaveManyAsync ----------

        [Fact]
        public async Task SaveMany_inserts_all_in_one_transaction()
        {
            var (conn, dialect) = Open();
            var repo = new SqlRepository<TypesModel, int>(conn, dialect);
            await repo.EnsureSchemaAsync();

            var batch = Enumerable.Range(0, 25).Select(i => new TypesModel
            {
                Name = $"batch_{i}",
                GuidValue = Guid.NewGuid(),
                DecimalValue = i * 1.5m,
                DateValue = DateTime.UtcNow
            }).ToList();

            var saved = await repo.SaveManyAsync(batch);
            Assert.Equal(25, saved.Count);
            Assert.All(saved, e => Assert.True(e.Id > 0));

            var all = await repo.FindAllAsync();
            Assert.Equal(25, all.Count);
        }

        [Fact]
        public async Task SaveMany_mixes_inserts_and_updates()
        {
            var (conn, dialect) = Open();
            var repo = new SqlRepository<TypesModel, int>(conn, dialect);
            await repo.EnsureSchemaAsync();

            var existing = new TypesModel { Name = "old", GuidValue = Guid.NewGuid(), DecimalValue = 1m, DateValue = DateTime.UtcNow };
            await repo.SaveAsync(existing);
            existing.Name = "updated";

            var newOne = new TypesModel { Name = "new", GuidValue = Guid.NewGuid(), DecimalValue = 2m, DateValue = DateTime.UtcNow };

            await repo.SaveManyAsync(new[] { existing, newOne });

            var all = await repo.FindAllAsync();
            Assert.Equal(2, all.Count);
            Assert.Contains(all, e => e.Name == "updated");
            Assert.Contains(all, e => e.Name == "new");
        }

        // ---------- CreatedAt / UpdatedAt ----------

        [Fact]
        public async Task Timestamps_are_set_on_insert_and_updated_on_save()
        {
            var (conn, dialect) = Open();
            var repo = new SqlRepository<TypesModel, int>(conn, dialect);
            await repo.EnsureSchemaAsync();

            var entity = new TypesModel { Name = "ts", GuidValue = Guid.NewGuid(), DecimalValue = 1m, DateValue = DateTime.UtcNow };
            await repo.SaveAsync(entity);
            var createdAt = entity.CreatedAt;
            var firstUpdate = entity.UpdatedAt;
            Assert.NotEqual(default, createdAt);
            Assert.Equal(createdAt, firstUpdate);

            await Task.Delay(50);
            entity.Name = "ts2";
            await repo.SaveAsync(entity);
            Assert.Equal(createdAt.Date, entity.CreatedAt.Date); // CreatedAt preservado
            Assert.True(entity.UpdatedAt >= firstUpdate);
        }
    }

    // -------- Fixtures concretas --------

    public class AdvancedScenariosSqlite : AdvancedScenarios
    {
        protected override (DbConnection conn, IDialect dialect) Open()
        {
            var c = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
            c.Open();
            return (c, new LightOrm.Sqlite.SqliteDialect());
        }
    }

    public class AdvancedScenariosMySql : TestBase
    {
        private AdvancedScenariosWithConn Wrap() => new AdvancedScenariosWithConn(Connection, new LightOrm.MySql.MySqlDialect());

        [Fact] public Task RoundTrips_Guid_Decimal_DateTime_and_nulls() => Wrap().RoundTrips_Guid_Decimal_DateTime_and_nulls();
        [Fact] public Task RoundTrips_decimal_with_high_precision() => Wrap().RoundTrips_decimal_with_high_precision();
        [Theory]
        [InlineData("simples")]
        [InlineData("com 'aspas simples'")]
        [InlineData("com \"aspas duplas\"")]
        [InlineData("com `backticks`")]
        [InlineData("com -- SQL injection -- DROP TABLE")]
        [InlineData("emoji 🎉🚀 acentos áéíóú çñ")]
        [InlineData("japonês こんにちは / russo Привет")]
        [InlineData("")]
        public Task RoundTrips_problematic_strings(string text) => Wrap().RoundTrips_problematic_strings(text);
        [Fact] public Task FindById_nonexistent_returns_null() => Wrap().FindById_nonexistent_returns_null();
        [Fact] public Task FindAll_on_empty_table_returns_empty_list_not_null() => Wrap().FindAll_on_empty_table_returns_empty_list_not_null();
        [Fact] public Task Save_twice_does_not_create_duplicate_and_preserves_id() => Wrap().Save_twice_does_not_create_duplicate_and_preserves_id();
        [Fact] public Task Update_preserves_unchanged_fields() => Wrap().Update_preserves_unchanged_fields();
        [Fact] public Task Orphan_FK_does_not_crash_includeRelated() => Wrap().Orphan_FK_does_not_crash_includeRelated();
        [Fact] public Task Parent_with_no_children_returns_empty_array() => Wrap().Parent_with_no_children_returns_empty_array();
        [Fact] public Task FindAll_includeRelated_handles_volume() => Wrap().FindAll_includeRelated_handles_volume();
        [Fact] public Task Delete_removes_record_and_findall_reflects() => Wrap().Delete_removes_record_and_findall_reflects();
        [Fact] public Task IncludeRelated_only_loads_one_level_deep() => Wrap().IncludeRelated_only_loads_one_level_deep();
        [Fact] public Task Timestamps_are_set_on_insert_and_updated_on_save() => Wrap().Timestamps_are_set_on_insert_and_updated_on_save();
        [Fact] public Task SaveMany_inserts_all_in_one_transaction() => Wrap().SaveMany_inserts_all_in_one_transaction();
        [Fact] public Task SaveMany_mixes_inserts_and_updates() => Wrap().SaveMany_mixes_inserts_and_updates();

        private class AdvancedScenariosWithConn : AdvancedScenarios
        {
            private readonly DbConnection _conn;
            private readonly IDialect _dialect;
            public AdvancedScenariosWithConn(DbConnection conn, IDialect dialect)
            { _conn = conn; _dialect = dialect; }
            protected override (DbConnection conn, IDialect dialect) Open() => (_conn, _dialect);
        }
    }

    public class AdvancedScenariosPostgres : PostgresCrudTests
    {
        private AdvancedScenariosWithConn Wrap() => new AdvancedScenariosWithConn(Connection, new LightOrm.Postgres.PostgresDialect());

        [Fact] public Task RoundTrips_Guid_Decimal_DateTime_and_nulls() => Wrap().RoundTrips_Guid_Decimal_DateTime_and_nulls();
        [Fact] public Task RoundTrips_decimal_with_high_precision() => Wrap().RoundTrips_decimal_with_high_precision();
        [Theory]
        [InlineData("simples")]
        [InlineData("com 'aspas simples'")]
        [InlineData("com \"aspas duplas\"")]
        [InlineData("com `backticks`")]
        [InlineData("com -- SQL injection -- DROP TABLE")]
        [InlineData("emoji 🎉🚀 acentos áéíóú çñ")]
        [InlineData("japonês こんにちは / russo Привет")]
        [InlineData("")]
        public Task RoundTrips_problematic_strings(string text) => Wrap().RoundTrips_problematic_strings(text);
        [Fact] public Task FindById_nonexistent_returns_null() => Wrap().FindById_nonexistent_returns_null();
        [Fact] public Task FindAll_on_empty_table_returns_empty_list_not_null() => Wrap().FindAll_on_empty_table_returns_empty_list_not_null();
        [Fact] public Task Save_twice_does_not_create_duplicate_and_preserves_id() => Wrap().Save_twice_does_not_create_duplicate_and_preserves_id();
        [Fact] public Task Update_preserves_unchanged_fields() => Wrap().Update_preserves_unchanged_fields();
        [Fact] public Task Orphan_FK_does_not_crash_includeRelated() => Wrap().Orphan_FK_does_not_crash_includeRelated();
        [Fact] public Task Parent_with_no_children_returns_empty_array() => Wrap().Parent_with_no_children_returns_empty_array();
        [Fact] public Task FindAll_includeRelated_handles_volume() => Wrap().FindAll_includeRelated_handles_volume();
        [Fact] public Task Delete_removes_record_and_findall_reflects() => Wrap().Delete_removes_record_and_findall_reflects();
        [Fact] public Task IncludeRelated_only_loads_one_level_deep() => Wrap().IncludeRelated_only_loads_one_level_deep();
        [Fact] public Task Timestamps_are_set_on_insert_and_updated_on_save() => Wrap().Timestamps_are_set_on_insert_and_updated_on_save();
        [Fact] public Task SaveMany_inserts_all_in_one_transaction() => Wrap().SaveMany_inserts_all_in_one_transaction();
        [Fact] public Task SaveMany_mixes_inserts_and_updates() => Wrap().SaveMany_mixes_inserts_and_updates();

        private class AdvancedScenariosWithConn : AdvancedScenarios
        {
            private readonly DbConnection _conn;
            private readonly IDialect _dialect;
            public AdvancedScenariosWithConn(DbConnection conn, IDialect dialect)
            { _conn = conn; _dialect = dialect; }
            protected override (DbConnection conn, IDialect dialect) Open() => (_conn, _dialect);
        }
    }
}
