using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using LightOrm.Core.Sql;
using LightOrm.Core.Tests.Models;
using LightOrm.MySql;
using MySql.Data.MySqlClient;
using Xunit;

namespace LightOrm.Core.Tests
{
    public class SqlInjectionTests : TestBase
    {
        [Fact]
        public async Task MaliciousTableNameIsNeutralized()
        {
            await EnsureSentinelUsersTableAsync();

            var repo = new SqlRepository<MaliciousTableNameModel, int>(Connection, new MySqlDialect());
            await repo.EnsureSchemaAsync();

            await AssertUsersTableStillExists();
        }

        [Fact]
        public async Task MaliciousColumnNameIsNeutralized()
        {
            await EnsureSentinelUsersTableAsync();

            var repo = new SqlRepository<MaliciousColumnNameModel, int>(Connection, new MySqlDialect());
            await repo.EnsureSchemaAsync();

            await AssertUsersTableStillExists();
        }

        [Fact]
        public async Task Where_value_payload_is_parameterized_and_does_not_expand_match()
        {
            var repo = await CreateUserRepoAsync();

            var payload = "safe-user' OR 1=1 --";
            var found = await repo.Query()
                .Where(nameof(TestUserModel.UserName), payload)
                .ToListAsync();

            Assert.Empty(found);
            await AssertUsersTableStillExists();
            await AssertTestUsersRowCountAsync(2);
        }

        [Fact]
        public async Task Where_column_name_value_payload_is_parameterized_and_does_not_expand_match()
        {
            var repo = await CreateUserRepoAsync();

            var payload = "other-user' UNION SELECT 1,2,3 --";
            var found = await repo.Query()
                .Where("user_name", payload)
                .ToListAsync();

            Assert.Empty(found);
            await AssertUsersTableStillExists();
            await AssertTestUsersRowCountAsync(2);
        }

        [Fact]
        public async Task WhereIn_payload_is_parameterized_and_does_not_expand_match()
        {
            var repo = await CreateUserRepoAsync();

            var payload = "u1') OR 1=1 --";
            var found = await repo.Query()
                .WhereIn(nameof(TestUserModel.UserName), new object[] { payload })
                .ToListAsync();

            Assert.Empty(found);
            await AssertUsersTableStillExists();
            await AssertTestUsersRowCountAsync(2);
        }

        [Fact]
        public async Task WhereAny_payloads_are_parameterized_and_only_legit_branch_matches()
        {
            var repo = await CreateUserRepoAsync();

            var found = await repo.Query()
                .WhereAny(
                    (nameof(TestUserModel.UserName), "=", "safe-user"),
                    (nameof(TestUserModel.UserName), "=", "safe-user' OR 1=1 --"),
                    (nameof(TestUserModel.EmailAddress), "LIKE", "%' UNION SELECT password FROM users --"))
                .ToListAsync();

            Assert.Single(found);
            Assert.Equal("safe-user", found[0].UserName);
            await AssertUsersTableStillExists();
        }

        [Fact]
        public async Task Like_payload_is_parameterized_and_does_not_match_everything()
        {
            var repo = await CreateUserRepoAsync();

            var found = await repo.Query()
                .Where(nameof(TestUserModel.UserName), "LIKE", "%' OR 1=1 --")
                .ToListAsync();

            Assert.Empty(found);
            await AssertUsersTableStillExists();
            await AssertTestUsersRowCountAsync(2);
        }

        [Fact]
        public async Task OrderBy_payload_is_rejected_before_sql_execution()
        {
            var repo = await CreateUserRepoAsync();

            var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
                repo.Query()
                    .OrderBy("user_name DESC; DROP TABLE users; --")
                    .ToListAsync());

            Assert.Contains("nao encontrada", RemoveDiacritics(ex.Message), StringComparison.OrdinalIgnoreCase);
            await AssertUsersTableStillExists();
            await AssertTestUsersRowCountAsync(2);
        }

        [Fact]
        public async Task Operator_payload_is_rejected_before_sql_execution()
        {
            var repo = await CreateUserRepoAsync();

            var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
                repo.Query()
                    .Where(nameof(TestUserModel.UserName), "= 'x'; DROP TABLE users; --", "safe-user")
                    .ToListAsync());

            Assert.Contains("nao suportado", RemoveDiacritics(ex.Message), StringComparison.OrdinalIgnoreCase);
            await AssertUsersTableStillExists();
            await AssertTestUsersRowCountAsync(2);
        }

        [Fact]
        public async Task Bulk_update_value_payload_is_parameterized_and_does_not_execute_extra_sql()
        {
            var repo = await CreateUserRepoAsync();

            var payload = "renamed', is_active = 0; DROP TABLE users; --";
            var affected = await repo.Query()
                .Where(nameof(TestUserModel.UserName), "safe-user")
                .UpdateAsync(new Dictionary<string, object> { [nameof(TestUserModel.UserName)] = payload });

            Assert.Equal(1, affected);

            var loaded = await repo.Query()
                .Where(nameof(TestUserModel.UserName), payload)
                .FirstOrDefaultAsync();
            Assert.NotNull(loaded);
            Assert.True(loaded.IsActive);
            await AssertUsersTableStillExists();
            await AssertTestUsersRowCountAsync(2);
        }

        [Fact]
        public async Task Bulk_update_malicious_property_name_is_rejected()
        {
            var repo = await CreateUserRepoAsync();

            await Assert.ThrowsAsync<ArgumentException>(() =>
                repo.Query()
                    .Where(nameof(TestUserModel.UserName), "safe-user")
                    .UpdateAsync(new Dictionary<string, object>
                    {
                        ["user_name = 'pwned'; DROP TABLE users; --"] = "x"
                    }));

            await AssertUsersTableStillExists();
            await AssertTestUsersRowCountAsync(2);
        }

        [Fact]
        public async Task Bulk_delete_payload_does_not_delete_extra_rows()
        {
            var repo = await CreateUserRepoAsync();

            var deleted = await repo.Query()
                .Where(nameof(TestUserModel.UserName), "safe-user' OR 1=1 --")
                .DeleteAsync();

            Assert.Equal(0, deleted);
            await AssertUsersTableStillExists();
            await AssertTestUsersRowCountAsync(2);
        }

        [Fact]
        public async Task Raw_query_with_parameters_treats_payload_as_data()
        {
            var repo = await CreateUserRepoAsync();

            var payload = "safe-user' OR 1=1 --";
            var rows = await repo.RawAsync(
                "SELECT * FROM test_users WHERE user_name = @name",
                new Dictionary<string, object> { ["name"] = payload });

            Assert.Empty(rows);
            await AssertUsersTableStillExists();
            await AssertTestUsersRowCountAsync(2);
        }

        [Fact]
        public async Task Raw_query_with_prefixed_parameter_name_treats_payload_as_data()
        {
            var repo = await CreateUserRepoAsync();

            var payload = "other-user'; DELETE FROM test_users; --";
            var rows = await repo.RawAsync(
                "SELECT * FROM test_users WHERE user_name = @name",
                new Dictionary<string, object> { ["@name"] = payload });

            Assert.Empty(rows);
            await AssertUsersTableStillExists();
            await AssertTestUsersRowCountAsync(2);
        }

        [Fact]
        public async Task Stored_payload_round_trips_as_plain_data_without_side_effects()
        {
            var repo = await CreateUserRepoAsync();

            var payload = "x'); DROP TABLE users; -- \n /* attacker */";
            await repo.SaveAsync(new TestUserModel
            {
                UserName = payload,
                EmailAddress = "payload@x.com",
                IsActive = true
            });

            var loaded = await repo.Query()
                .Where(nameof(TestUserModel.UserName), payload)
                .FirstOrDefaultAsync();

            Assert.NotNull(loaded);
            Assert.Equal(payload, loaded.UserName);
            await AssertUsersTableStillExists();
            await AssertTestUsersRowCountAsync(3);
        }

        private async Task<SqlRepository<TestUserModel, int>> CreateUserRepoAsync()
        {
            await EnsureSentinelUsersTableAsync();

            var repo = new SqlRepository<TestUserModel, int>(Connection, new MySqlDialect());
            await repo.EnsureSchemaAsync();
            await repo.SaveAsync(new TestUserModel { UserName = "safe-user", EmailAddress = "safe@x.com", IsActive = true });
            await repo.SaveAsync(new TestUserModel { UserName = "other-user", EmailAddress = "other@x.com", IsActive = true });
            return repo;
        }

        private async Task EnsureSentinelUsersTableAsync()
        {
            using var setup = new MySqlCommand("CREATE TABLE IF NOT EXISTS users (id INT);", Connection);
            await setup.ExecuteNonQueryAsync();
        }

        private async Task AssertUsersTableStillExists()
        {
            using var check = new MySqlCommand("SHOW TABLES LIKE 'users';", Connection);
            using var reader = await check.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync(), "Tabela 'users' nao deveria ter sido dropada.");
        }

        private async Task AssertTestUsersRowCountAsync(int expected)
        {
            using var cmd = new MySqlCommand("SELECT COUNT(*) FROM test_users;", Connection);
            var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            Assert.Equal(expected, count);
        }

        private static string RemoveDiacritics(string value)
        {
            if (string.IsNullOrEmpty(value)) return value;
            var normalized = value.Normalize(NormalizationForm.FormD);
            var chars = new List<char>(normalized.Length);
            foreach (var c in normalized)
            {
                if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
                    chars.Add(c);
            }
            return new string(chars.ToArray()).Normalize(NormalizationForm.FormC);
        }
    }
}
