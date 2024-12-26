using System;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;
using LightOrm.Tests.Models;

namespace LightOrm.Tests
{
    public class ConfigurationTests
    {
        private readonly MySqlConnection _connection;

        public ConfigurationTests(string connectionString)
        {
            _connection = new MySqlConnection(connectionString);
        }

        public async Task RunAllTests()
        {
            try
            {
                await _connection.OpenAsync();
                await InitializeDatabaseAsync();
                await TestDefaultValuesAsync();
                await TestEnumConstraintAsync();
                await TestCheckConstraintsAsync();
                await TestUniqueConstraintAsync();
                await TestNullableColumnsAsync();
                await TestTimestampColumnsAsync();
            }
            finally
            {
                if (_connection.State != System.Data.ConnectionState.Closed)
                {
                    await _connection.CloseAsync();
                }
            }
        }

        private async Task InitializeDatabaseAsync()
        {
            Console.WriteLine("Initializing configuration test table...");

            // Drop and recreate table
            using (var cmd = new MySqlCommand(
                "DROP TABLE IF EXISTS configuration_tests", _connection))
            {
                await cmd.ExecuteNonQueryAsync();
            }

            await new ConfigurationTestModel().EnsureTableExistsAsync(_connection);
            Console.WriteLine("Table created successfully!");
        }

        private async Task TestDefaultValuesAsync()
        {
            Console.WriteLine("\nTesting default values...");

            var model = new ConfigurationTestModel
            {
                Name = "Test User",
                Email = "test@example.com"
            };

            await model.SaveAsync(_connection);
            Console.WriteLine("Created model with default values");

            var loaded = await ConfigurationTestModel.FindByIdAsync(_connection, model.Id);
            Console.WriteLine($"Status: {loaded.Status} (expected: active)");
            Console.WriteLine($"Balance: {loaded.Balance} (expected: 0.00)");
            Console.WriteLine($"Age: {loaded.Age} (expected: 0)");
            Console.WriteLine($"IsActive: {loaded.IsActive} (expected: true)");
            Console.WriteLine($"Notes: '{loaded.Notes}' (expected: '')");
            Console.WriteLine($"Score: {loaded.Score} (expected: 0)");
        }

        private async Task TestEnumConstraintAsync()
        {
            Console.WriteLine("\nTesting enum constraint...");

            try
            {
                var model = new ConfigurationTestModel
                {
                    Name = "Invalid Status",
                    Email = "invalid@example.com",
                    Status = "invalid_status" // Should fail
                };
                await model.SaveAsync(_connection);
                Console.WriteLine("ERROR: Should not allow invalid status");
            }
            catch (MySqlException ex)
            {
                Console.WriteLine($"Successfully prevented invalid status: {ex.Message}");
            }
        }

        private async Task TestCheckConstraintsAsync()
        {
            Console.WriteLine("\nTesting check constraints...");

            try
            {
                var model = new ConfigurationTestModel
                {
                    Name = "Invalid Age",
                    Email = "age@example.com",
                    Age = 200 // Should fail
                };
                await model.SaveAsync(_connection);
                Console.WriteLine("ERROR: Should not allow invalid age");
            }
            catch (MySqlException ex)
            {
                Console.WriteLine($"Successfully prevented invalid age: {ex.Message}");
            }

            try
            {
                var model = new ConfigurationTestModel
                {
                    Name = "Invalid Score",
                    Email = "score@example.com",
                    Score = 150 // Should fail
                };
                await model.SaveAsync(_connection);
                Console.WriteLine("ERROR: Should not allow invalid score");
            }
            catch (MySqlException ex)
            {
                Console.WriteLine($"Successfully prevented invalid score: {ex.Message}");
            }
        }

        private async Task TestUniqueConstraintAsync()
        {
            Console.WriteLine("\nTesting unique constraint...");

            var model1 = new ConfigurationTestModel
            {
                Name = "User 1",
                Email = "duplicate@example.com"
            };
            await model1.SaveAsync(_connection);
            Console.WriteLine("Created first user");

            try
            {
                var model2 = new ConfigurationTestModel
                {
                    Name = "User 2",
                    Email = "duplicate@example.com" // Should fail
                };
                await model2.SaveAsync(_connection);
                Console.WriteLine("ERROR: Should not allow duplicate email");
            }
            catch (MySqlException ex)
            {
                Console.WriteLine($"Successfully prevented duplicate email: {ex.Message}");
            }
        }

        private async Task TestNullableColumnsAsync()
        {
            Console.WriteLine("\nTesting nullable columns...");

            try
            {
                var model = new ConfigurationTestModel
                {
                    Email = "null@example.com",
                    Name = null // Should fail
                };
                await model.SaveAsync(_connection);
                Console.WriteLine("ERROR: Should not allow null name");
            }
            catch (MySqlException ex)
            {
                Console.WriteLine($"Successfully prevented null name: {ex.Message}");
            }

            var validModel = new ConfigurationTestModel
            {
                Name = "Nullable Test",
                Email = "nullable@example.com",
                LastLogin = null // Should work
            };
            await validModel.SaveAsync(_connection);
            Console.WriteLine("Successfully saved model with null LastLogin");
        }

        private async Task TestTimestampColumnsAsync()
        {
            Console.WriteLine("\nTesting timestamp columns...");

            var model = new ConfigurationTestModel
            {
                Name = "Timestamp Test",
                Email = "time@example.com"
            };
            await model.SaveAsync(_connection);
            Console.WriteLine("Created model, checking timestamps...");

            var loaded = await ConfigurationTestModel.FindByIdAsync(_connection, model.Id);
            var initialCreatedAt = loaded.CreatedAt;
            var initialUpdatedAt = loaded.UpdatedAt;
            Console.WriteLine($"Initial CreatedAt: {initialCreatedAt:yyyy-MM-dd HH:mm:ss.fff}");
            Console.WriteLine($"Initial UpdatedAt: {initialUpdatedAt:yyyy-MM-dd HH:mm:ss.fff}");

            // Wait a second to see the difference
            await Task.Delay(1000);

            loaded.Name = "Updated Name";
            await loaded.SaveAsync(_connection);
            Console.WriteLine("\nUpdated model, checking timestamps...");

            var reloaded = await ConfigurationTestModel.FindByIdAsync(_connection, model.Id);
            Console.WriteLine($"CreatedAt: {reloaded.CreatedAt:yyyy-MM-dd HH:mm:ss.fff}");
            Console.WriteLine($"UpdatedAt: {reloaded.UpdatedAt:yyyy-MM-dd HH:mm:ss.fff}");

            // Verify timestamps
            if (reloaded.CreatedAt == initialCreatedAt)
            {
                Console.WriteLine("✓ CreatedAt remained unchanged");
            }
            else
            {
                Console.WriteLine($"✗ CreatedAt changed unexpectedly");
                Console.WriteLine($"  Original: {initialCreatedAt:yyyy-MM-dd HH:mm:ss.fff}");
                Console.WriteLine($"  Current:  {reloaded.CreatedAt:yyyy-MM-dd HH:mm:ss.fff}");
            }

            if (reloaded.UpdatedAt > initialUpdatedAt)
            {
                Console.WriteLine("✓ UpdatedAt was automatically updated");
                Console.WriteLine($"  Time difference: {(reloaded.UpdatedAt - initialUpdatedAt).TotalSeconds:F3} seconds");
            }
            else
            {
                Console.WriteLine($"✗ UpdatedAt did not update as expected");
                Console.WriteLine($"  Original: {initialUpdatedAt:yyyy-MM-dd HH:mm:ss.fff}");
                Console.WriteLine($"  Current:  {reloaded.UpdatedAt:yyyy-MM-dd HH:mm:ss.fff}");
            }
        }
    }
}
