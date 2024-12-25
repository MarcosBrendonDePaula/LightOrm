using System;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;
using LightOrm.Tests.Models;

namespace LightOrm.Tests
{
    class Program
    {
        static async Task Main(string[] args)
        {
            try
            {
                // Configure database connection
                var builder = new MySqlConnectionStringBuilder
                {
                    Server = "37.60.241.117",
                    Database = "teste",
                    UserID = "admin",
                    Password = "Melissa5",
                    Port = 3309
                };

                // Run relationship tests
                var tests = new RelationshipTests(builder.ConnectionString);
                await tests.RunAllTests();

                Console.WriteLine("\nAll tests completed successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }

            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }
    }
}
