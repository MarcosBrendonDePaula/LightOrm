#if !NO_UNITY
using System;
using System.Data.Common;
using System.IO;
using System.Threading.Tasks;
using LightOrm.Core.Models;
using LightOrm.Core.Sql;
using LightOrm.MySql;
using LightOrm.Postgres;
using LightOrm.Sqlite;
using Microsoft.Data.Sqlite;
using MySql.Data.MySqlClient;
using Npgsql;
using UnityEngine;

namespace LightOrm.Unity.Database
{
    /// <summary>
    /// MonoBehaviour singleton que centraliza a configuração do banco em um
    /// jogo Unity. Suporta SQLite (default — save local), MySQL e Postgres via
    /// dropdown no Inspector. Devolve repositórios já configurados via
    /// GetRepository&lt;T, TId&gt;() — sem precisar instanciar SqlRepository
    /// na mão.
    ///
    /// Uso típico:
    ///   var repo = await DatabaseManager.Instance.GetRepositoryAsync&lt;UserModel, int&gt;();
    ///   await repo.EnsureSchemaAsync();
    ///   await repo.SaveAsync(new UserModel { Name = "..." });
    /// </summary>
    public class DatabaseManager : MonoBehaviour
    {
        public enum DbProvider { Sqlite, MySql, Postgres }

        private static DatabaseManager _instance;
        public static DatabaseManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("DatabaseManager");
                    _instance = go.AddComponent<DatabaseManager>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        [Header("Provider")]
        [SerializeField] private DbProvider provider = DbProvider.Sqlite;

        [Header("SQLite")]
        [Tooltip("Nome do arquivo dentro de Application.persistentDataPath. " +
                 "Vazio = usa 'lightorm.db'.")]
        [SerializeField] private string sqliteFileName = "lightorm.db";
        [Tooltip("Se true, usa banco em memória (perdido ao fechar). Útil para testes.")]
        [SerializeField] private bool sqliteInMemory = false;

        [Header("MySQL / Postgres")]
        [SerializeField] private string server = "localhost";
        [SerializeField] private string database = "your_database";
        [SerializeField] private string userId = "your_user";
        [SerializeField] private string password = "your_password";
        [SerializeField] private string port = "3306";
        [SerializeField] private bool pooling = true;
        [SerializeField] private bool useSsl = false;

        private DbConnection _connection;
        private IDialect _dialect;
        private bool _initialized;

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _connection?.Dispose();
                _connection = null;
                _instance = null;
                _initialized = false;
            }
        }

        /// <summary>
        /// Garante connection aberta + dialect resolvido. Chame antes de pegar
        /// repositórios. Idempotente — chamadas subsequentes são no-op.
        /// </summary>
        public async Task InitializeAsync()
        {
            if (_initialized) return;

            try
            {
                switch (provider)
                {
                    case DbProvider.Sqlite:
                        var sqlitePath = sqliteInMemory
                            ? "Data Source=:memory:"
                            : "Data Source=" + Path.Combine(Application.persistentDataPath,
                                  string.IsNullOrEmpty(sqliteFileName) ? "lightorm.db" : sqliteFileName);
                        _connection = new SqliteConnection(sqlitePath);
                        _dialect = new SqliteDialect();
                        break;

                    case DbProvider.MySql:
                        _connection = new MySqlConnection(BuildMySqlConnectionString());
                        _dialect = new MySqlDialect();
                        break;

                    case DbProvider.Postgres:
                        _connection = new NpgsqlConnection(BuildPostgresConnectionString());
                        _dialect = new PostgresDialect();
                        break;

                    default:
                        throw new InvalidOperationException($"Provider não suportado: {provider}");
                }

                await _connection.OpenAsync();
                _initialized = true;
                Debug.Log($"[DatabaseManager] Conectado via {provider}.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DatabaseManager] Falha ao inicializar: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Retorna repositório SQL configurado. Garante inicialização antes.
        /// Cada chamada cria nova instância de SqlRepository, mas a connection
        /// é compartilhada (cuidado em multi-thread — Unity é single-thread).
        /// </summary>
        public async Task<SqlRepository<T, TId>> GetRepositoryAsync<T, TId>()
            where T : BaseModel<T, TId>, new()
        {
            await InitializeAsync();
            return new SqlRepository<T, TId>(_connection, _dialect);
        }

        /// <summary>
        /// Versão síncrona. Lança se ainda não inicializado — chame
        /// InitializeAsync() em algum Start() async antes.
        /// </summary>
        public SqlRepository<T, TId> GetRepository<T, TId>()
            where T : BaseModel<T, TId>, new()
        {
            if (!_initialized)
                throw new InvalidOperationException(
                    "DatabaseManager ainda não foi inicializado. " +
                    "Chame await InitializeAsync() antes ou use GetRepositoryAsync().");
            return new SqlRepository<T, TId>(_connection, _dialect);
        }

        /// <summary>Connection bruta — caso o dev queira controle direto.</summary>
        public DbConnection Connection
        {
            get
            {
                if (!_initialized)
                    throw new InvalidOperationException("DatabaseManager não inicializado.");
                return _connection;
            }
        }

        /// <summary>Dialect ativo — caso o dev queira passar para um repo customizado.</summary>
        public IDialect Dialect
        {
            get
            {
                if (!_initialized)
                    throw new InvalidOperationException("DatabaseManager não inicializado.");
                return _dialect;
            }
        }

        // ------- helpers -------

        private string BuildMySqlConnectionString()
        {
            var b = new MySqlConnectionStringBuilder
            {
                Server = server,
                Database = database,
                UserID = userId,
                Password = password,
                Port = uint.Parse(port),
                Pooling = pooling,
                SslMode = useSsl ? MySqlSslMode.Preferred : MySqlSslMode.Disabled
            };
            return b.ConnectionString;
        }

        private string BuildPostgresConnectionString()
        {
            var b = new NpgsqlConnectionStringBuilder
            {
                Host = server,
                Database = database,
                Username = userId,
                Password = password,
                Port = int.Parse(port),
                Pooling = pooling
            };
            if (useSsl) b.SslMode = SslMode.Prefer;
            return b.ConnectionString;
        }
    }
}
#endif
