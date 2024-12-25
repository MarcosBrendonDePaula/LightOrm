using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Generic;
using MySql.Data.MySqlClient;
using LightOrm.Tests.Models.RelationshipExamples;

namespace LightOrm.Tests
{
    public class PerformanceTests
    {
        private readonly MySqlConnection _connection;
        private readonly Stopwatch _stopwatch;

        public PerformanceTests(MySqlConnection connection)
        {
            _connection = connection;
            _stopwatch = new Stopwatch();
        }

        public async Task RunAllTests()
        {
            Console.WriteLine("\nRunning Performance Tests...\n");

            await InitializeDatabaseAsync();
            await TestSingleEntityPerformanceAsync();
            await TestBatchLoadingPerformanceAsync();
            await TestCacheInvalidationPerformanceAsync();
            await TestRelationshipLoadingPerformanceAsync();
        }

        private async Task InitializeDatabaseAsync()
        {
            Console.WriteLine("Initializing test data...");

            // Create test models
            var student = new StudentModel
            {
                Name = "Performance Test Student",
                Email = "test@example.com"
            };
            await student.SaveAsync(_connection);

            // Create 100 assignments for performance testing
            for (int i = 0; i < 100; i++)
            {
                var assignment = new AssignmentModel
                {
                    Title = $"Assignment {i}",
                    Description = $"Test assignment {i}",
                    DueDate = DateTime.Now.AddDays(i),
                    Score = 85 + (i % 15),
                    StudentId = student.Id
                };
                await assignment.SaveAsync(_connection);
            }
        }

        private async Task TestSingleEntityPerformanceAsync()
        {
            Console.WriteLine("\nTesting Single Entity Performance:");

            // First load (no cache)
            _stopwatch.Restart();
            var student = await StudentModel.FindByIdAsync(_connection, 1);
            _stopwatch.Stop();
            Console.WriteLine($"First load (no cache): {_stopwatch.ElapsedMilliseconds}ms");

            // Second load (with cache)
            _stopwatch.Restart();
            student = await StudentModel.FindByIdAsync(_connection, 1);
            _stopwatch.Stop();
            Console.WriteLine($"Second load (with cache): {_stopwatch.ElapsedMilliseconds}ms");

            // Load with relationships (no cache)
            _stopwatch.Restart();
            student = await StudentModel.FindByIdAsync(_connection, 1, includeRelated: true);
            _stopwatch.Stop();
            Console.WriteLine($"Load with relationships (no cache): {_stopwatch.ElapsedMilliseconds}ms");

            // Load with relationships (with cache)
            _stopwatch.Restart();
            student = await StudentModel.FindByIdAsync(_connection, 1, includeRelated: true);
            _stopwatch.Stop();
            Console.WriteLine($"Load with relationships (with cache): {_stopwatch.ElapsedMilliseconds}ms");
        }

        private async Task TestBatchLoadingPerformanceAsync()
        {
            Console.WriteLine("\nTesting Batch Loading Performance:");

            // First batch load (no cache)
            _stopwatch.Restart();
            var assignments = await AssignmentModel.FindAllAsync(_connection);
            _stopwatch.Stop();
            Console.WriteLine($"First batch load (no cache): {_stopwatch.ElapsedMilliseconds}ms for {assignments.Count} items");

            // Second batch load (with cache)
            _stopwatch.Restart();
            assignments = await AssignmentModel.FindAllAsync(_connection);
            _stopwatch.Stop();
            Console.WriteLine($"Second batch load (with cache): {_stopwatch.ElapsedMilliseconds}ms for {assignments.Count} items");
        }

        private async Task TestCacheInvalidationPerformanceAsync()
        {
            Console.WriteLine("\nTesting Cache Invalidation Performance:");

            // Load entity
            var student = await StudentModel.FindByIdAsync(_connection, 1);

            // Update entity
            _stopwatch.Restart();
            student.Name = "Updated Name";
            await student.SaveAsync(_connection);
            _stopwatch.Stop();
            Console.WriteLine($"Update with cache invalidation: {_stopwatch.ElapsedMilliseconds}ms");

            // Load after update (should get from DB)
            _stopwatch.Restart();
            student = await StudentModel.FindByIdAsync(_connection, 1);
            _stopwatch.Stop();
            Console.WriteLine($"Load after update: {_stopwatch.ElapsedMilliseconds}ms");

            // Load again (should get from cache)
            _stopwatch.Restart();
            student = await StudentModel.FindByIdAsync(_connection, 1);
            _stopwatch.Stop();
            Console.WriteLine($"Load from new cache: {_stopwatch.ElapsedMilliseconds}ms");
        }

        private async Task TestRelationshipLoadingPerformanceAsync()
        {
            Console.WriteLine("\nTesting Relationship Loading Performance:");

            // Create test data with relationships
            var address = new AddressModel
            {
                Street = "123 Performance St",
                City = "Test City",
                State = "TS",
                PostalCode = "12345"
            };
            await address.SaveAsync(_connection);

            var student = await StudentModel.FindByIdAsync(_connection, 1);
            student.AddressId = address.Id;
            await student.SaveAsync(_connection);

            // First load with relationships (no cache)
            _stopwatch.Restart();
            var result = await StudentModel.FindByIdAsync(_connection, 1, includeRelated: true);
            _stopwatch.Stop();
            Console.WriteLine($"First load with relationships (no cache): {_stopwatch.ElapsedMilliseconds}ms");

            // Second load with relationships (partial cache)
            _stopwatch.Restart();
            result = await StudentModel.FindByIdAsync(_connection, 1, includeRelated: true);
            _stopwatch.Stop();
            Console.WriteLine($"Second load with relationships (partial cache): {_stopwatch.ElapsedMilliseconds}ms");

            // Third load with relationships (full cache)
            _stopwatch.Restart();
            result = await StudentModel.FindByIdAsync(_connection, 1, includeRelated: true);
            _stopwatch.Stop();
            Console.WriteLine($"Third load with relationships (full cache): {_stopwatch.ElapsedMilliseconds}ms");
        }
    }
}
