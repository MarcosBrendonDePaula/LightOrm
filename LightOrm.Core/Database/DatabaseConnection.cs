using MySql.Data.MySqlClient;
using System;

namespace LightOrm.Core.Database
{
    public class DatabaseConnection
    {
        private string _connectionString;
        private readonly Action<string> _logger;
        private readonly Action<string> _errorLogger;

        public DatabaseConnection(
            string server,
            string database,
            string userId,
            string password,
            string port = "3306",
            bool pooling = true,
            bool useSsl = false,
            Action<string> logger = null,
            Action<string> errorLogger = null)
        {
            _logger = logger ?? (msg => { });
            _errorLogger = errorLogger ?? (msg => { });
            InitializeConnectionString(server, database, userId, password, port, pooling, useSsl);
        }

        private void InitializeConnectionString(
            string server,
            string database,
            string userId,
            string password,
            string port,
            bool pooling,
            bool useSsl)
        {
            try
            {
                var builder = new MySqlConnectionStringBuilder
                {
                    Server = server,
                    Database = database,
                    UserID = userId,
                    Password = password,
                    Port = UInt32.Parse(port),
                    Pooling = pooling,
                    SslMode = useSsl ? MySqlSslMode.Preferred : MySqlSslMode.None
                };

                _connectionString = builder.ConnectionString;
                _logger("Database connection string initialized successfully.");
            }
            catch (Exception ex)
            {
                _errorLogger($"Error initializing connection string: {ex.Message}");
                throw;
            }
        }

        public string ConnectionString => _connectionString;

        public MySqlConnection GetConnection()
        {
            if (string.IsNullOrEmpty(_connectionString))
            {
                _errorLogger("Connection string is not initialized.");
                throw new InvalidOperationException("Connection string is not initialized.");
            }

            return new MySqlConnection(_connectionString);
        }
    }
}
