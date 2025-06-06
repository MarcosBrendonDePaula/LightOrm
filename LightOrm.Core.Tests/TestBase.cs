using MySql.Data.MySqlClient;
using System.Threading.Tasks;
using Xunit;
using System.Threading;
using System;

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
            _uniqueTestDbName = $"testdb_{Guid.NewGuid().ToString("N")}";
            _testDbConnectionString = $"Server=localhost;Port=3307;Database={_uniqueTestDbName};Uid=root;Pwd=my-secret-pw;";
        }

        public async Task InitializeAsync()
        {
            Console.WriteLine($"InitializeAsync started for {_uniqueTestDbName}.");
            // Connect as root to create the test database
            using (var rootConnection = new MySqlConnection(_rootConnectionString))
            {
                Console.WriteLine($"Opening root connection to {_rootConnectionString}");
                await rootConnection.OpenAsync();
                Console.WriteLine("Root connection opened.");
                using (var cmd = new MySqlCommand($"CREATE DATABASE IF NOT EXISTS {_uniqueTestDbName};", rootConnection))
                {
                    Console.WriteLine($"Executing CREATE DATABASE IF NOT EXISTS {_uniqueTestDbName};");
                    await cmd.ExecuteNonQueryAsync();
                    Console.WriteLine("CREATE DATABASE command executed.");
                }
                // Add a small delay to ensure the database is fully created and visible
                Console.WriteLine("Waiting 500ms...");
                await Task.Delay(500); 
                Console.WriteLine("Wait finished.");
            }

            // Now connect to the specific test database
            Console.WriteLine($"Opening testdb connection to {_testDbConnectionString}");
            Connection = new MySqlConnection(_testDbConnectionString);
            await Connection.OpenAsync();
            Console.WriteLine($"Connected to database: {Connection.Database}");
            Console.WriteLine("InitializeAsync finished.");
        }

        public async Task DisposeAsync()
        {
            Console.WriteLine($"DisposeAsync started for {_uniqueTestDbName}.");
            if (Connection != null && Connection.State == System.Data.ConnectionState.Open)
            {
                Console.WriteLine("Closing testdb connection.");
                await Connection.CloseAsync();
                Connection.Dispose();
                Console.WriteLine("Testdb connection closed.");
            }

            // Connect as root to drop the test database
            using (var rootConnection = new MySqlConnection(_rootConnectionString))
            {
                Console.WriteLine("Opening root connection to drop testdb.");
                await rootConnection.OpenAsync();
                Console.WriteLine("Root connection opened for dropping testdb.");
                using (var cmd = new MySqlCommand($"DROP DATABASE IF EXISTS {_uniqueTestDbName};", rootConnection))
                {
                    Console.WriteLine($"Executing DROP DATABASE IF EXISTS {_uniqueTestDbName};");
                    await cmd.ExecuteNonQueryAsync();
                    Console.WriteLine("DROP DATABASE command executed.");
                }
            }
            Console.WriteLine("DisposeAsync finished.");
        }
    }
}

