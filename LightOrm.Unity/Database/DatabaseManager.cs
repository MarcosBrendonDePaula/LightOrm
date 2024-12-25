using UnityEngine;
using System;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;

namespace LightOrm.Unity.Database
{
    public class DatabaseManager : MonoBehaviour
    {
        private static DatabaseManager _instance;
        public static DatabaseManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    GameObject dbManagerObject = new GameObject("DatabaseManager");
                    _instance = dbManagerObject.AddComponent<DatabaseManager>();
                    DontDestroyOnLoad(dbManagerObject);
                }
                return _instance;
            }
        }

        [Header("Database Connection Settings")]
        [SerializeField] private string server = "localhost";
        [SerializeField] private string database = "your_database";
        [SerializeField] private string userId = "your_user";
        [SerializeField] private string password = "your_password";
        [SerializeField] private string port = "3306";
        [SerializeField] private bool pooling = true;
        [SerializeField] private bool useSsl = false;

        private string _connectionString;

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeConnectionString();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void InitializeConnectionString()
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
                Debug.Log("[DatabaseManager] Connection string initialized successfully.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DatabaseManager] Error initializing connection string: {ex.Message}");
                throw;
            }
        }

        public MySqlConnection GetConnection()
        {
            if (string.IsNullOrEmpty(_connectionString))
            {
                Debug.LogError("[DatabaseManager] Connection string is not initialized.");
                throw new InvalidOperationException("Connection string is not initialized.");
            }

            return new MySqlConnection(_connectionString);
        }

        public string ConnectionString => _connectionString;

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }
    }
}
