# LightOrm

ORM leve em C# com suporte a múltiplos bancos via abstração `IRepository<T, TId>` + `IDialect`. Hoje cobre **MySQL**, **SQLite** e **MongoDB**, e funciona em aplicações .NET Standard 2.1 e Unity.

## Arquitetura

- `LightOrm.Core` — `BaseModel<T, TId>` (metadados), `IRepository<T, TId>`, atributos, `SqlRepository<T, TId>` agnóstico de provider, `IDialect`. Zero dependência de driver.
- `LightOrm.MySql` — `MySqlDialect` + helper `DatabaseConnection` (MySql.Data).
- `LightOrm.Sqlite` — `SqliteDialect` (Microsoft.Data.Sqlite).
- `LightOrm.Mongo` — `MongoRepository<T, TId>` sobre `IMongoCollection` (MongoDB.Driver).
- `LightOrm.Unity` — `DatabaseManager` MonoBehaviour para integração no Editor.

Modelos descrevem dados via atributos; a operação CRUD vive no repositório, não no modelo.

## Instalação

Referencie o `LightOrm.Core` mais o(s) provider(s) que você precisa:

- MySQL: `LightOrm.Core` + `LightOrm.MySql`
- SQLite: `LightOrm.Core` + `LightOrm.Sqlite`
- MongoDB: `LightOrm.Core` + `LightOrm.Mongo`

Para Unity, copie os DLLs compilados (`LightOrm.Core.dll`, `LightOrm.Unity.dll`, `LightOrm.MySql.dll`, `MySql.Data.dll`) para `Assets/Plugins`.

## Definindo modelos

```csharp
using LightOrm.Core.Models;
using LightOrm.Core.Attributes;

public class UserModel : BaseModel<UserModel, int>
{
    public override string TableName => "users";

    [Column("name", length: 100)]
    public string Name { get; set; }

    [Column("email", length: 255)]
    public string Email { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; }
}
```

`BaseModel<T, TId>` já fornece `Id` (genérico), `CreatedAt` e `UpdatedAt`. O segundo parâmetro define o tipo da chave: `int`/`long`/`Guid` para SQL, `string` (ObjectId) para Mongo.

### Relacionamentos (SQL)

```csharp
public class PostModel : BaseModel<PostModel, int>
{
    public override string TableName => "posts";

    [Column("title", length: 200)]
    public string Title { get; set; }

    [Column("user_id")]
    [ForeignKey("users")]
    public int UserId { get; set; }

    [OneToOne("UserId", typeof(UserModel))]
    public UserModel User { get; set; }
}

// Em UserModel:
[OneToMany("user_id", typeof(PostModel))]
public PostModel[] Posts { get; set; }
```

`SqlRepository` carrega navigation properties quando `includeRelated: true`. O loader resolve N+1 fazendo uma query por relacionamento com `WHERE pk IN (...)`. Atributos relacionais são ignorados pelo `MongoRepository`.

## Usando o repositório

### MySQL

```csharp
using LightOrm.Core.Sql;
using LightOrm.MySql;
using MySql.Data.MySqlClient;

using var conn = new MySqlConnection("Server=localhost;Database=app;Uid=root;Pwd=...");
var repo = new SqlRepository<UserModel, int>(conn, new MySqlDialect());

await repo.EnsureSchemaAsync();

var user = new UserModel { Name = "Ana", Email = "ana@x.com", IsActive = true };
await repo.SaveAsync(user);                  // INSERT — popula user.Id
user.Email = "ana@y.com";
await repo.SaveAsync(user);                  // UPDATE

var loaded = await repo.FindByIdAsync(user.Id, includeRelated: true);
var all = await repo.FindAllAsync();
await repo.DeleteAsync(loaded);
```

### SQLite (in-memory ou arquivo)

```csharp
using Microsoft.Data.Sqlite;
using LightOrm.Sqlite;

using var conn = new SqliteConnection("Data Source=:memory:");
conn.Open();
var repo = new SqlRepository<UserModel, int>(conn, new SqliteDialect());
await repo.EnsureSchemaAsync();
// ...mesma API.
```

### MongoDB

```csharp
using MongoDB.Driver;
using LightOrm.Mongo;

// Para Mongo, defina o modelo com TId = string (ObjectId).
public class UserMongoModel : BaseModel<UserMongoModel, string> { ... }

var db = new MongoClient("mongodb://localhost:27017").GetDatabase("app");
var repo = new MongoRepository<UserMongoModel, string>(db);
await repo.EnsureSchemaAsync();              // no-op (schemaless)

var user = new UserMongoModel { Name = "Ana" };
await repo.SaveAsync(user);                  // gera ObjectId em user.Id
```

## Unity

```csharp
public class GameManager : MonoBehaviour
{
    private async void Start()
    {
        using var conn = DatabaseManager.Instance.GetConnection();
        var repo = new SqlRepository<UserModel, int>(conn, new MySqlDialect());

        var user = new UserModel { Name = "Player", Email = "p@game.com", IsActive = true };
        await repo.SaveAsync(user);
        Debug.Log($"Created user {user.Id}");
    }
}
```

Configure servidor/usuário/senha no Inspector do `DatabaseManager`.

## Build e testes

```
dotnet build LightOrm.sln
dotnet test  LightOrm.Core.Tests/LightOrm.Core.Tests.csproj
```

Os testes assumem MySQL local em `localhost:3307` (root/`my-secret-pw`) e MongoDB em `localhost:27017`. SQLite roda in-memory sem dependência externa. Suba os serviços via Docker:

```
docker run -d --name lightorm-mysql -p 3307:3306 -e MYSQL_ROOT_PASSWORD=my-secret-pw mysql:8
docker run -d --name lightorm-mongo -p 27017:27017 mongo:7
```

## Limitações conhecidas

- Sem cascata em `SaveAsync`/`DeleteAsync` — gerencie FKs manualmente.
- Carregamento eager apenas (`includeRelated: true`); não há lazy loading.
- `MongoRepository` não traduz atributos relacionais (planejado: embed via novo `[Embedded]`).
- Sem query builder fluente; consultas além de `FindById`/`FindAll` exigem SQL/BSON manual.

## Licença

MIT.
