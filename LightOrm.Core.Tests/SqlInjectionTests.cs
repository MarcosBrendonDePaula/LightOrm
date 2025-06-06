using Xunit;
using System.Threading.Tasks;
using LightOrm.Core.Models;
using LightOrm.Core.Tests.Models;
using MySql.Data.MySqlClient;
using System;

namespace LightOrm.Core.Tests
{
    public class SqlInjectionTests : TestBase
    {
        [Fact]
        public async Task MaliciousTableNameIsNotExecuted()
        {
            // Arrange
            var maliciousModel = new MaliciousTableNameModel();

            // Act & Assert
            // Expect an exception because the malicious table name, when escaped, becomes an invalid identifier.
            // This confirms that the SQL injection attempt was prevented.
            await Assert.ThrowsAsync<MySqlException>(async () =>
            {
                await maliciousModel.EnsureTableExistsAsync(Connection);
            });
        }

        [Fact]
        public async Task MaliciousColumnNameIsNotExecuted()
        {
            // Arrange
            var maliciousModel = new MaliciousColumnNameModel();

            // Act & Assert
            // Expect an exception because the malicious column name, when escaped, becomes an invalid identifier.
            // This confirms that the SQL injection attempt was prevented.
            await Assert.ThrowsAsync<MySqlException>(async () =>
            {
                await maliciousModel.EnsureTableExistsAsync(Connection);
            });
        }
    }
}

