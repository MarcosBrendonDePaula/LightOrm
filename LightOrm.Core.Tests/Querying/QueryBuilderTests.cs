using System;
using System.Threading.Tasks;
using LightOrm.Core.Sql;
using LightOrm.Core.Tests.Models;
using LightOrm.Sqlite;
using Microsoft.Data.Sqlite;
using Xunit;

namespace LightOrm.Core.Tests
{
    public class QueryBuilderTests
    {
        private static (SqliteConnection conn, IDialect dialect) Open()
        {
            var c = new SqliteConnection("Data Source=:memory:");
            c.Open();
            return (c, new SqliteDialect());
        }

        private async Task<SqlRepository<TypesModel, int>> Seed()
        {
            var (conn, dialect) = Open();
            var repo = new SqlRepository<TypesModel, int>(conn, dialect);
            await repo.EnsureSchemaAsync();
            for (int i = 1; i <= 10; i++)
            {
                await repo.SaveAsync(new TypesModel
                {
                    Name = $"item-{i:D2}",
                    GuidValue = Guid.NewGuid(),
                    DecimalValue = i * 1.5m,
                    DateValue = DateTime.UtcNow,
                    NullableInt = i % 2 == 0 ? (int?)i : null
                });
            }
            return repo;
        }

        [Fact]
        public async Task Where_equals_filters_correctly()
        {
            var repo = await Seed();
            var result = await repo.Query()
                .Where(nameof(TypesModel.Name), "item-05")
                .ToListAsync();
            Assert.Single(result);
            Assert.Equal("item-05", result[0].Name);
        }

        [Fact]
        public async Task Where_with_operator_supports_gt_and_lt()
        {
            var repo = await Seed();
            var greater = await repo.Query()
                .Where(nameof(TypesModel.DecimalValue), ">", 7.5m)
                .ToListAsync();
            Assert.All(greater, e => Assert.True(e.DecimalValue > 7.5m));

            var lessOrEq = await repo.Query()
                .Where(nameof(TypesModel.DecimalValue), "<=", 3m)
                .ToListAsync();
            Assert.All(lessOrEq, e => Assert.True(e.DecimalValue <= 3m));
        }

        [Fact]
        public async Task WhereIn_handles_multiple_and_empty_lists()
        {
            var repo = await Seed();
            var some = await repo.Query()
                .WhereIn(nameof(TypesModel.Name), new object[] { "item-01", "item-05", "item-09" })
                .ToListAsync();
            Assert.Equal(3, some.Count);

            var none = await repo.Query()
                .WhereIn(nameof(TypesModel.Name), Array.Empty<object>())
                .ToListAsync();
            Assert.Empty(none);
        }

        [Fact]
        public async Task OrderBy_and_OrderByDescending_work()
        {
            var repo = await Seed();
            var asc = await repo.Query()
                .OrderBy(nameof(TypesModel.DecimalValue))
                .ToListAsync();
            for (int i = 1; i < asc.Count; i++)
                Assert.True(asc[i - 1].DecimalValue <= asc[i].DecimalValue);

            var desc = await repo.Query()
                .OrderByDescending(nameof(TypesModel.DecimalValue))
                .ToListAsync();
            for (int i = 1; i < desc.Count; i++)
                Assert.True(desc[i - 1].DecimalValue >= desc[i].DecimalValue);
        }

        [Fact]
        public async Task Take_and_Skip_paginate()
        {
            var repo = await Seed();
            var page = await repo.Query()
                .OrderBy(nameof(TypesModel.Name))
                .Skip(3)
                .Take(2)
                .ToListAsync();
            Assert.Equal(2, page.Count);
            Assert.Equal("item-04", page[0].Name);
            Assert.Equal("item-05", page[1].Name);
        }

        [Fact]
        public async Task FirstOrDefault_returns_null_when_no_match()
        {
            var repo = await Seed();
            var nope = await repo.Query()
                .Where(nameof(TypesModel.Name), "inexistente")
                .FirstOrDefaultAsync();
            Assert.Null(nope);

            var found = await repo.Query()
                .Where(nameof(TypesModel.Name), "item-03")
                .FirstOrDefaultAsync();
            Assert.NotNull(found);
        }

        [Fact]
        public async Task Count_and_Any_work()
        {
            var repo = await Seed();
            var total = await repo.Query().CountAsync();
            Assert.Equal(10, total);

            var filtered = await repo.Query()
                .Where(nameof(TypesModel.DecimalValue), ">", 7.5m)
                .CountAsync();
            Assert.True(filtered > 0 && filtered < 10);

            Assert.True(await repo.Query().AnyAsync());
            Assert.False(await repo.Query()
                .Where(nameof(TypesModel.Name), "inexistente")
                .AnyAsync());
        }

        [Fact]
        public async Task Like_operator_works()
        {
            var repo = await Seed();
            var result = await repo.Query()
                .Where(nameof(TypesModel.Name), "LIKE", "item-0%")
                .ToListAsync();
            Assert.Equal(9, result.Count); // item-01..09
        }

        [Fact]
        public async Task Invalid_property_name_throws_clear_error()
        {
            var repo = await Seed();
            var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
                repo.Query().Where("PropriedadeQueNaoExiste", 1).ToListAsync());
            Assert.Contains("PropriedadeQueNaoExiste", ex.Message);
            Assert.Contains("TypesModel", ex.Message);
        }

        [Fact]
        public async Task Invalid_operator_throws_clear_error()
        {
            var repo = await Seed();
            var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
                repo.Query().Where(nameof(TypesModel.Name), "; DROP TABLE x", "x").ToListAsync());
            Assert.Contains("não suportado", ex.Message);
        }

        [Fact]
        public async Task WhereAny_combines_conditions_with_OR()
        {
            var repo = await Seed();
            var result = await repo.Query()
                .WhereAny(
                    (nameof(TypesModel.Name), "=", "item-01"),
                    (nameof(TypesModel.Name), "=", "item-05"),
                    (nameof(TypesModel.Name), "=", "item-09"))
                .ToListAsync();
            Assert.Equal(3, result.Count);
        }

        [Fact]
        public async Task WhereAny_combined_with_Where_uses_AND_between_them()
        {
            var repo = await Seed();
            var result = await repo.Query()
                .Where(nameof(TypesModel.DecimalValue), ">", 5m)
                .WhereAny(
                    (nameof(TypesModel.Name), "=", "item-04"),  // dec=6.0 — passa só o segundo
                    (nameof(TypesModel.Name), "=", "item-08"))  // dec=12.0 — passa
                .ToListAsync();
            // item-04 tem decimal 4*1.5=6 > 5 ✓; item-08 tem 12 > 5 ✓.
            Assert.Equal(2, result.Count);
        }

        [Fact]
        public async Task WhereAny_with_empty_throws()
        {
            var repo = await Seed();
            await Assert.ThrowsAsync<ArgumentException>(() =>
                repo.Query().WhereAny().ToListAsync());
        }

        [Fact]
        public async Task Multiple_Where_combine_with_AND()
        {
            var repo = await Seed();
            var result = await repo.Query()
                .Where(nameof(TypesModel.DecimalValue), ">", 3m)
                .Where(nameof(TypesModel.DecimalValue), "<", 9m)
                .ToListAsync();
            Assert.All(result, e => Assert.True(e.DecimalValue > 3m && e.DecimalValue < 9m));
        }

        [Fact]
        public async Task FirstOrDefault_does_not_poison_reused_query_limit()
        {
            var repo = await Seed();
            var query = repo.Query()
                .Where(nameof(TypesModel.DecimalValue), ">", 0m)
                .OrderBy(nameof(TypesModel.Name));

            var first = await query.FirstOrDefaultAsync();
            var all = await query.ToListAsync();

            Assert.NotNull(first);
            Assert.Equal(10, all.Count);
            Assert.Equal("item-01", all[0].Name);
            Assert.Equal("item-10", all[9].Name);
        }

        [Fact]
        public async Task Any_does_not_poison_reused_query_limit()
        {
            var repo = await Seed();
            var query = repo.Query()
                .Where(nameof(TypesModel.DecimalValue), ">", 0m)
                .OrderBy(nameof(TypesModel.Name));

            Assert.True(await query.AnyAsync());

            var all = await query.ToListAsync();
            Assert.Equal(10, all.Count);
        }
    }
}
