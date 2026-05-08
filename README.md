# LightOrm

ORM leve em C# com suporte a múltiplos bancos via abstração `IRepository<T, TId>` + `IDialect`. Hoje cobre **MySQL**, **SQLite**, **PostgreSQL** e **MongoDB**, e funciona em aplicações .NET Standard 2.1 e Unity.

## Arquitetura

- `LightOrm.Core` — `BaseModel<T, TId>` (metadados), `IRepository<T, TId>`, atributos, `SqlRepository<T, TId>` agnóstico de provider, `IDialect`. Zero dependência de driver.
- `LightOrm.MySql` — `MySqlDialect` + helper `DatabaseConnection` (MySql.Data).
- `LightOrm.Sqlite` — `SqliteDialect` (Microsoft.Data.Sqlite).
- `LightOrm.Postgres` — `PostgresDialect` (Npgsql).
- `LightOrm.Mongo` — `MongoRepository<T, TId>` sobre `IMongoCollection` (MongoDB.Driver).
- `LightOrm.Unity` — `DatabaseManager` MonoBehaviour para integração no Editor.

Modelos descrevem dados via atributos; a operação CRUD vive no repositório, não no modelo.

## Instalação

Referencie o `LightOrm.Core` mais o(s) provider(s) que você precisa:

- MySQL: `LightOrm.Core` + `LightOrm.MySql`
- SQLite: `LightOrm.Core` + `LightOrm.Sqlite`
- PostgreSQL: `LightOrm.Core` + `LightOrm.Postgres`
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

### PostgreSQL

```csharp
using Npgsql;
using LightOrm.Postgres;

using var conn = new NpgsqlConnection("Host=localhost;Database=app;Username=postgres;Password=...");
var repo = new SqlRepository<UserModel, int>(conn, new PostgresDialect());
await repo.EnsureSchemaAsync();
// ...mesma API; AUTO_INCREMENT vira SERIAL/BIGSERIAL automaticamente.
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

## Modelo portável e troca de backend

A interface `IRepository<T, TId>` é a mesma em todos os providers. Programe contra ela e o composition root escolhe o backend:

```csharp
public class User : BaseModel<User, string>   // string viabiliza SQL e Mongo no mesmo modelo
{
    public override string TableName => "users";
    [Column("name")]   public string Name { get; set; }
    [Column("active")] public bool   Active { get; set; }
}

IRepository<User, string> repo = backend switch
{
    "mysql"    => RepositoryFactory.Sql<User, string>(mysqlConn,    new MySqlDialect()),
    "sqlite"   => RepositoryFactory.Sql<User, string>(sqliteConn,   new SqliteDialect()),
    "postgres" => RepositoryFactory.Sql<User, string>(postgresConn, new PostgresDialect()),
    "mongo"    => new MongoRepository<User, string>(database),
    _ => throw new InvalidOperationException()
};

// O resto do código não muda em nenhum caso.
await repo.SaveAsync(user);
```

Quando `TId = string`, o framework gera `Guid.NewGuid().ToString("N")` no insert se você não preencher `Id`. Se preencher, use `UpsertAsync` em vez de `SaveAsync` (caso contrário vira UPDATE de linha que não existe).

## Query builder

`repo.Query()` devolve `IQuery<T, TId>` — mesma API em SQL e Mongo:

```csharp
var ativos = await repo.Query()
    .Where(nameof(User.Active), true)
    .Where(nameof(User.Name), "LIKE", "Ana%")
    .OrderByDescending(nameof(User.CreatedAt))
    .Take(20)
    .ToListAsync();

var quantos = await repo.Query()
    .WhereIn(nameof(User.Name), new object[] { "Ana", "Bia", "Caio" })
    .CountAsync();

var existe = await repo.Query()
    .Where(nameof(User.Name), "Ana")
    .AnyAsync();
```

Operadores: `=`, `!=`, `<>`, `<`, `<=`, `>`, `>=`, `LIKE`, `NOT LIKE`. Em Mongo, `LIKE` é traduzido para regex (`%` → `.*`, `_` → `.`).

Nomes de propriedade são resolvidos contra o modelo (use `nameof(...)`); valores desconhecidos viram `ArgumentException` antes de tocar o banco. Operadores fora da whitelist também são rejeitados.

### Grupos OR (`WhereAny`)

```csharp
var moderators = await repo.Query()
    .Where(nameof(User.Active), true)
    .WhereAny(
        (nameof(User.Role), "=", "admin"),
        (nameof(User.Role), "=", "moderator"))
    .ToListAsync();
// SQL:   WHERE active = ? AND (role = ? OR role = ?)
// Mongo: { active: true, $or: [{ role: 'admin' }, { role: 'moderator' }] }
```

## Upsert

Quando o caller controla o id (string/Guid):

```csharp
var u = new User { Id = "user-001", Name = "Ana" };
await repo.UpsertAsync(u);   // insere — id não existia
u.Name = "Ana Atualizada";
await repo.UpsertAsync(u);   // atualiza — preserva CreatedAt original
```

## Atributos de modelo

| Atributo | Onde |
|---|---|
| `[Table(name)]` | Define o nome da tabela/coleção sem precisar override de `TableName`. |
| `[Column(name, length, isPrimaryKey, autoIncrement, isUnsigned)]` | Mapeia propriedade para coluna/campo. Obrigatório nos campos persistidos. |
| `[ForeignKey(referenceTable, referenceColumn)]` | Gera `FOREIGN KEY` no `CREATE TABLE`. SQL apenas. |
| `[OneToOne(fkProperty, relatedType, cascade?)]` | Navigation property 1:1. `cascade: true` salva o filho junto. SQL apenas. |
| `[OneToMany(fkProperty, relatedType, cascade?)]` | Coleção 1:N. `cascade: true` salva os filhos junto. SQL apenas. |
| `[ManyToMany(relatedType, associationTable, sourceFK, targetFK)]` | N:N via tabela de associação. SQL apenas. |
| `[Embedded]` | Subdocumento aninhado (1:1 ou array 1:N). MongoDB apenas; ignorado em SQL. |
| `[Index(name?, unique?)]` | Cria índice em `EnsureSchemaAsync`. Mesmo `name` em várias propriedades = índice composto. SQL apenas. |
| `[Unique]` | Atalho para `UNIQUE INDEX` dedicado em uma coluna. SQL apenas. |
| `[Version]` | Optimistic locking em propriedade `int`/`long`. Update incrementa e checa versão; conflito lança `DbConcurrencyException`. SQL apenas. |

Subdocumentos `[Embedded]` são POCO comum — não precisam herdar `BaseModel`.

## Optimistic locking

```csharp
public class Order : BaseModel<Order, int>
{
    public override string TableName => "orders";
    [Column("total")] public decimal Total { get; set; }
    [Column("row_version")] [Version] public int RowVersion { get; set; }
}

try { await repo.SaveAsync(order); }
catch (DbConcurrencyException) {
    // outro processo modificou a linha desde que você leu — recarregue e tente de novo
}
```

Insert inicializa em 1; cada update incrementa e adiciona `AND row_version = @oldVersion` ao WHERE. Se nada for atualizado, lança e o valor em memória é revertido para o original (permite reload + retry sem estado inconsistente).

## Save em cascata

Opt-in via `cascade: true` no atributo de relacionamento. Salva pai e filhos numa única transação:

```csharp
public class Parent : BaseModel<Parent, int>
{
    public override string TableName => "parents";
    [Column("name")] public string Name { get; set; }

    [OneToMany("parent_id", typeof(Child), cascade: true)]
    public Child[] Children { get; set; }
}

var p = new Parent
{
    Name = "p",
    Children = new[] { new Child { Label = "c1" }, new Child { Label = "c2" } }
};
await parents.SaveAsync(p);
// p.Id preenchido; cada Child.Id preenchido; Child.ParentId = p.Id; tudo na mesma tx.
```

Se qualquer filho falhar, a transação inteira é revertida. Update misturando filhos novos e existentes funciona da mesma forma.

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

Os testes assumem MySQL local em `localhost:3307` (root/`my-secret-pw`), PostgreSQL em `localhost:5433` (postgres/`my-secret-pw`) e MongoDB em `localhost:27017`. SQLite roda in-memory sem dependência externa. Suba os serviços via Docker:

```
docker run -d --name lightorm-mysql    -p 3307:3306  -e MYSQL_ROOT_PASSWORD=my-secret-pw  mysql:8
docker run -d --name lightorm-postgres -p 5433:5432  -e POSTGRES_PASSWORD=my-secret-pw    postgres:16
docker run -d --name lightorm-mongo    -p 27017:27017                                     mongo:7
```

## Transações

Para operar várias entidades atomicamente, abra uma `DbTransaction` e passe para os repositórios:

```csharp
using var tx = connection.BeginTransaction();
var users = new SqlRepository<User, int>(connection, dialect, tx);
var orders = new SqlRepository<Order, int>(connection, dialect, tx);
await users.SaveAsync(...);
await orders.SaveAsync(...);
tx.Commit();   // ou tx.Rollback()
```

`SaveManyAsync` e `FindByIdAsync(includeRelated: true)` respeitam a transação ambiente.

## Batch save

`SaveManyAsync(entities)` insere/atualiza um conjunto numa única transação (uma chamada `InsertManyAsync` no Mongo para os novos):

```csharp
await repo.SaveManyAsync(new[] { user1, user2, user3 });
```

## Limitações conhecidas

- Cascata em `SaveAsync` é opt-in e não cobre `ManyToMany`. `DeleteAsync` ainda não tem cascata.
- Carregamento eager apenas (`includeRelated: true`); não há lazy loading.
- `MongoRepository` não traduz `[OneToMany]`/`[ManyToMany]` via `$lookup`. Use `[Embedded]` para relacionamentos no Mongo.
- Query builder cobre filtros simples (`Where`/`WhereIn`/`WhereAny`/`OrderBy`/`Take`/`Skip`/`Count`/`Any`); JOINs e GROUP BY exigem SQL manual.
- Optimistic locking via `[Version]` está disponível em SQL apenas; Mongo ainda não.

## Licença

MIT.
