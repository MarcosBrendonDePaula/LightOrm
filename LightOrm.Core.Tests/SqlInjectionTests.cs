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
        // Garante que tentativas de SQL injection no nome da tabela ou da coluna
        // são neutralizadas pelo escape de backticks no MySqlDialect, sem nunca
        // executar o payload (DROP TABLE) embutido.
        [Fact]
        public async Task MaliciousTableNameIsNeutralized()
        {
            using (var setup = new MySqlCommand("CREATE TABLE users (id INT);", Connection))
                await setup.ExecuteNonQueryAsync();

            var repo = new SqlRepository<MaliciousTableNameModel, int>(Connection, new MySqlDialect());
            await repo.EnsureSchemaAsync();

            using var check = new MySqlCommand("SHOW TABLES LIKE 'users';", Connection);
            using var reader = await check.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync(), "Tabela 'users' não deveria ter sido dropada.");
        }

        [Fact]
        public async Task MaliciousColumnNameIsNeutralized()
        {
            using (var setup = new MySqlCommand("CREATE TABLE users (id INT);", Connection))
                await setup.ExecuteNonQueryAsync();

            var repo = new SqlRepository<MaliciousColumnNameModel, int>(Connection, new MySqlDialect());
            await repo.EnsureSchemaAsync();

            using var check = new MySqlCommand("SHOW TABLES LIKE 'users';", Connection);
            using var reader = await check.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync(), "Tabela 'users' não deveria ter sido dropada.");
        }
    }
}
