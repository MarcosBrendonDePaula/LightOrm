using MySql.Data.MySqlClient;
using System;
using System.Threading.Tasks;
using Xunit;

namespace LightOrm.Core.Tests
{
    public abstract class TestBase : IAsyncLifetime
    {
        protected MySqlConnection Connection { get; private set; }
        private string _rootConnectionString;
        private string _uniqueTestDbName;
        private string _testDbConnectionString;

        public TestBase()
        {
            _rootConnectionString = "Server=localhost;Port=3307;Uid=root;Pwd=my-secret-pw;";
            _uniqueTestDbName = $"testdb_{Guid.NewGuid():N}";
            _testDbConnectionString = $"Server=localhost;Port=3307;Database={_uniqueTestDbName};Uid=root;Pwd=my-secret-pw;";
        }

        public async Task InitializeAsync()
        {
            using (var rootConnection = new MySqlConnection(_rootConnectionString))
            {
                await rootConnection.OpenAsync();
                using var cmd = new MySqlCommand($"CREATE DATABASE IF NOT EXISTS {_uniqueTestDbName};", rootConnection);
                await cmd.ExecuteNonQueryAsync();
            }

            Connection = new MySqlConnection(_testDbConnectionString);
            await Connection.OpenAsync();
        }

        public async Task DisposeAsync()
        {
            if (Connection != null && Connection.State == System.Data.ConnectionState.Open)
            {
                await Connection.CloseAsync();
                Connection.Dispose();
            }

            using var rootConnection = new MySqlConnection(_rootConnectionString);
            await rootConnection.OpenAsync();
            using var cmd = new MySqlCommand($"DROP DATABASE IF EXISTS {_uniqueTestDbName};", rootConnection);
            await cmd.ExecuteNonQueryAsync();
        }
    }
}
