using System;
using System.Threading.Tasks;
using LightOrm.Core.Sql;
using LightOrm.Core.Tests.Models;
using LightOrm.Sqlite;
using Microsoft.Data.Sqlite;
using Xunit;

namespace LightOrm.Core.Tests
{
    // Cenários de transações explícitas. Roda em SQLite (in-memory) — semântica
    // de tx é a mesma nos outros dialects e a propagação é coberta pelo design.
    public class TransactionTests
    {
        private static (SqliteConnection conn, IDialect dialect) Open()
        {
            var c = new SqliteConnection("Data Source=:memory:");
            c.Open();
            return (c, new SqliteDialect());
        }

        [Fact]
        public async Task Commit_persists_changes_across_repositories()
        {
            var (conn, dialect) = Open();
            // Schema sem tx ambiente.
            await new SqlRepository<TypesModel, int>(conn, dialect).EnsureSchemaAsync();
            await new SqlRepository<ParentModel, int>(conn, dialect).EnsureSchemaAsync();

            using (var tx = conn.BeginTransaction())
            {
                var typesRepo = new SqlRepository<TypesModel, int>(conn, dialect, tx);
                var parentsRepo = new SqlRepository<ParentModel, int>(conn, dialect, tx);

                await typesRepo.SaveAsync(new TypesModel
                {
                    Name = "tx-types",
                    GuidValue = Guid.NewGuid(),
                    DecimalValue = 10m,
                    DateValue = DateTime.UtcNow
                });
                await parentsRepo.SaveAsync(new ParentModel { Name = "tx-parent" });

                tx.Commit();
            }

            var types = await new SqlRepository<TypesModel, int>(conn, dialect).FindAllAsync();
            var parents = await new SqlRepository<ParentModel, int>(conn, dialect).FindAllAsync();
            Assert.Single(types);
            Assert.Single(parents);
        }

        [Fact]
        public async Task Rollback_discards_changes_from_all_repositories()
        {
            var (conn, dialect) = Open();
            await new SqlRepository<TypesModel, int>(conn, dialect).EnsureSchemaAsync();
            await new SqlRepository<ParentModel, int>(conn, dialect).EnsureSchemaAsync();

            using (var tx = conn.BeginTransaction())
            {
                var typesRepo = new SqlRepository<TypesModel, int>(conn, dialect, tx);
                var parentsRepo = new SqlRepository<ParentModel, int>(conn, dialect, tx);

                await typesRepo.SaveAsync(new TypesModel
                {
                    Name = "rollback-me",
                    GuidValue = Guid.NewGuid(),
                    DecimalValue = 10m,
                    DateValue = DateTime.UtcNow
                });
                await parentsRepo.SaveAsync(new ParentModel { Name = "rollback-me" });

                tx.Rollback();
            }

            var types = await new SqlRepository<TypesModel, int>(conn, dialect).FindAllAsync();
            var parents = await new SqlRepository<ParentModel, int>(conn, dialect).FindAllAsync();
            Assert.Empty(types);
            Assert.Empty(parents);
        }

        [Fact]
        public async Task SaveMany_inside_ambient_tx_does_not_open_inner_tx()
        {
            // Se SaveMany abrisse sua própria transação dentro de outra,
            // SQLite ou MySQL reclamariam "transaction already started".
            // Validamos que coexiste sem erro.
            var (conn, dialect) = Open();
            await new SqlRepository<TypesModel, int>(conn, dialect).EnsureSchemaAsync();

            using var tx = conn.BeginTransaction();
            var repo = new SqlRepository<TypesModel, int>(conn, dialect, tx);

            var batch = new[]
            {
                new TypesModel { Name = "a", GuidValue = Guid.NewGuid(), DecimalValue = 1m, DateValue = DateTime.UtcNow },
                new TypesModel { Name = "b", GuidValue = Guid.NewGuid(), DecimalValue = 2m, DateValue = DateTime.UtcNow },
                new TypesModel { Name = "c", GuidValue = Guid.NewGuid(), DecimalValue = 3m, DateValue = DateTime.UtcNow }
            };
            await repo.SaveManyAsync(batch);
            tx.Commit();

            var all = await new SqlRepository<TypesModel, int>(conn, dialect).FindAllAsync();
            Assert.Equal(3, all.Count);
        }

        [Fact]
        public async Task Find_with_includeRelated_under_ambient_tx_works()
        {
            var (conn, dialect) = Open();
            await new SqlRepository<AddressModel, int>(conn, dialect).EnsureSchemaAsync();
            await new SqlRepository<CourseModel, int>(conn, dialect).EnsureSchemaAsync();
            await new SqlRepository<StudentModel, int>(conn, dialect).EnsureSchemaAsync();
            await new SqlRepository<AssignmentModel, int>(conn, dialect).EnsureSchemaAsync();
            await new SqlRepository<StudentCourseLink, int>(conn, dialect).EnsureSchemaAsync();

            using var tx = conn.BeginTransaction();
            var addresses = new SqlRepository<AddressModel, int>(conn, dialect, tx);
            var students = new SqlRepository<StudentModel, int>(conn, dialect, tx);
            var assignments = new SqlRepository<AssignmentModel, int>(conn, dialect, tx);

            var addr = await addresses.SaveAsync(new AddressModel { Street = "R", City = "X" });
            var stu = await students.SaveAsync(new StudentModel { Name = "Ana", AddressId = addr.Id });
            await assignments.SaveAsync(new AssignmentModel { Title = "T1", StudentId = stu.Id });

            // Leitura dentro da própria tx — vê os dados mesmo antes do commit.
            var loaded = await students.FindByIdAsync(stu.Id, includeRelated: true);
            Assert.NotNull(loaded.Address);
            Assert.Single(loaded.Assignments);

            tx.Commit();
        }
    }
}
