using System;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;

namespace LightOrm.Tests
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var builder = new MySqlConnectionStringBuilder
            {
                Server = "37.60.241.117",
                Database = "teste",
                UserID = "admin",
                Password = "Melissa5",
                Port = 3309
            };

            using var connection = new MySqlConnection(builder.ConnectionString);
            await connection.OpenAsync();

            // Run relationship tests
            Console.WriteLine("Running Relationship Tests...");
            var relationshipTests = new RelationshipTests(builder.ConnectionString);
            await relationshipTests.RunAllTests();

            // Run performance tests
            Console.WriteLine("\nRunning Performance Tests...");
            var performanceTests = new PerformanceTests(connection);
            await performanceTests.RunAllTests();

            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }
    }
}
