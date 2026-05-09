# LightOrm

ORM leve em C# que roda em **MySQL**, **SQLite**, **PostgreSQL** e **MongoDB** com a mesma API. Funciona em aplicações .NET Standard 2.1 e Unity.

```csharp
public class User : BaseModel<User, int>
{
    public override string TableName => "users";

    [Column("name", length: 100)]
    [Required]
    public string Name { get; set; }

    [Column("email", length: 200)]
    [Unique]
    public string Email { get; set; }
}

var repo = new SqlRepository<User, int>(conn, new SqliteDialect());
await repo.EnsureSchemaAsync();
await repo.SaveAsync(new User { Name = "Ana", Email = "ana@x.com" });

var ativos = await repo.Query()
    .Where(nameof(User.Email), "LIKE", "%@x.com")
    .OrderBy(nameof(User.Name))
    .Take(20)
    .ToListAsync();
```

**295 testes verdes em 4 backends.**

---

## Sumário

- [Instalação](#instalação)
- [Arquitetura](#arquitetura)
- [Modelo portável (1 modelo, vários backends)](#modelo-portável-1-modelo-vários-backends)
- [Atributos de modelo](#atributos-de-modelo)
- [Repositório (`IRepository`)](#repositório-irepository)
- [Query builder (`IQuery`)](#query-builder-iquery)
- [Aggregations](#aggregations)
- [Scopes nomeados](#scopes-nomeados)
- [Bulk update / delete](#bulk-update--delete)
- [Hooks de ciclo de vida](#hooks-de-ciclo-de-vida)
- [Validação declarativa](#validação-declarativa)
- [`HookContext` — side effects multi-tabela](#hookcontext--side-effects-multi-tabela)
- [Cascade save & delete](#cascade-save--delete)
- [Eager loading multi-nível](#eager-loading-multi-nível)
- [Soft delete](#soft-delete)
- [Optimistic locking (`[Version]`)](#optimistic-locking-version)
- [Embedded (Mongo)](#embedded-mongo)
- [Transações](#transações)
- [Raw queries](#raw-queries)
- [Migrations](#migrations)
- [Unity](#unity)
- [Build e testes](#build-e-testes)
- [Limitações conhecidas](#limitações-conhecidas)

---

## Instalação

Referencie o `LightOrm.Core` mais o(s) provider(s) que você precisa:

| Backend | Pacote |
|---|---|
| MySQL | `LightOrm.Core` + `LightOrm.MySql` |
| SQLite | `LightOrm.Core` + `LightOrm.Sqlite` |
| PostgreSQL | `LightOrm.Core` + `LightOrm.Postgres` |
| MongoDB | `LightOrm.Core` + `LightOrm.Mongo` |
| Unity | + `LightOrm.Unity` (helpers MonoBehaviour) |

Para Unity, copie os DLLs compilados (`LightOrm.Core.dll`, `LightOrm.Sqlite.dll`/etc., drivers nativos) para `Assets/Plugins`.

---

## Arquitetura

- `LightOrm.Core` — `BaseModel<T, TId>`, `IRepository<T, TId>`, `IQuery<T, TId>`, atributos, validação, migrations, `SqlRepository<T, TId>` agnóstico de provider, `IDialect`. Zero dependência de driver.
- `LightOrm.MySql` — `MySqlDialect` (MySql.Data 8).
- `LightOrm.Sqlite` — `SqliteDialect` (Microsoft.Data.Sqlite).
- `LightOrm.Postgres` — `PostgresDialect` (Npgsql).
- `LightOrm.Mongo` — `MongoRepository<T, TId>` sobre `IMongoCollection` (MongoDB.Driver).
- `LightOrm.Unity` — `DatabaseManager` MonoBehaviour para integração no Editor.

Modelos descrevem dados via atributos; CRUD vive no repositório, não no modelo.

---

## Modelo portável (1 modelo, vários backends)

A interface `IRepository<T, TId>` é a mesma em todos os providers. Programe contra ela e o composition root escolhe o backend:

```csharp
public class User : BaseModel<User, string>     // string viabiliza SQL e Mongo no mesmo modelo
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

`IQuery<T, TId>` (devolvido por `repo.Query()`) também é portável — a mesma chamada funciona em SQL e Mongo.

---

## Atributos de modelo

| Atributo | Onde |
|---|---|
| `[Table(name)]` | Define o nome da tabela/coleção sem precisar override de `TableName`. |
| `[Column(name, length, isPrimaryKey, autoIncrement, isUnsigned)]` | Mapeia propriedade para coluna/campo. Obrigatório em campos persistidos. |
| `[ForeignKey(referenceTable, referenceColumn)]` | Gera `FOREIGN KEY` no `CREATE TABLE`. SQL apenas. |
| `[OneToOne(fkProperty, relatedType, cascade?, cascadeDelete?)]` | Navigation property 1:1. SQL apenas. |
| `[OneToMany(fkProperty, relatedType, cascade?, cascadeDelete?)]` | Coleção 1:N. SQL apenas. |
| `[ManyToMany(relatedType, associationTable, sourceFK, targetFK)]` | N:N via tabela de associação. SQL apenas. |
| `[Embedded]` | Subdocumento aninhado (1:1 ou array 1:N). MongoDB apenas; ignorado em SQL. |
| `[Index(name?, unique?)]` | Cria índice em `EnsureSchemaAsync`. Mesmo `name` em várias propriedades = índice composto. SQL apenas. |
| `[Unique]` | Atalho para `UNIQUE INDEX` dedicado em uma coluna. SQL apenas. |
| `[Version]` | Optimistic locking em `int`/`long`. Update incrementa e checa versão; conflito lança `DbConcurrencyException`. SQL e Mongo. |
| `[SoftDelete(columnName?)]` | Ativa soft delete na classe (default `deleted_at`). SQL e Mongo. |
| `[Scope("nome")]` | Marca método estático como scope reutilizável. |
| `[Required]` / `[MaxLength]` / `[MinLength]` / `[RegEx]` / `[Range]` | Validação declarativa. |

Subdocumentos `[Embedded]` são POCO comum — não precisam herdar `BaseModel`.

`BaseModel<T, TId>` já fornece de graça:
- `TId Id` (auto-incrementa em SQL para int/long; gerado com Guid em string).
- `DateTime CreatedAt` / `DateTime UpdatedAt` (preenchidos automaticamente).
- `DateTime? DeletedAt` (só vira coluna quando `[SoftDelete]` está ativo).

---

## Repositório (`IRepository`)

API completa em `IRepository<T, TId>`:

```csharp
Task EnsureSchemaAsync();
Task<T> SaveAsync(T entity);                              // insert ou update
Task<IReadOnlyList<T>> SaveManyAsync(IEnumerable<T>);     // batch transacional
Task<T> UpsertAsync(T entity);                            // insert-or-update por id
Task<T> FindByIdAsync(TId id, bool includeRelated = false);
Task<List<T>> FindAllAsync(bool includeRelated = false);
Task DeleteAsync(T entity);
IQuery<T, TId> Query();
Task<(T entity, bool created)> FindOrCreateAsync(Action<IQuery<T, TId>> filter, T defaults);
```

Adicional em `SqlRepository`: `Task<List<T>> RawAsync(sql, parameters?)`.

Adicional quando `[SoftDelete]` está ativo: `RestoreAsync(entity)`, `FindByIdIncludingDeletedAsync(id)`, `FindAllIncludingDeletedAsync()`.

---

## Query builder (`IQuery`)

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

### Operações de execução

| Método | Retorno |
|---|---|
| `ToListAsync()` | `List<T>` |
| `FirstOrDefaultAsync()` | `T?` |
| `CountAsync()` | `int` |
| `AnyAsync()` | `bool` |
| `SumAsync(prop)` / `AvgAsync(prop)` / `MinAsync(prop)` / `MaxAsync(prop)` | `decimal?` |
| `GroupByAsync(prop)` | `List<(object key, int count)>` |
| `UpdateAsync(setMap)` | `int` (linhas afetadas) |
| `DeleteAsync()` | `int` |

---

## Aggregations

```csharp
var total      = await repo.Query().SumAsync(nameof(Order.Total));
var media      = await repo.Query().AvgAsync(nameof(Order.Total));
var maisCaro   = await repo.Query().MaxAsync(nameof(Order.Total));
var maisBarato = await repo.Query().MinAsync(nameof(Order.Total));

var porStatus = await repo.Query().GroupByAsync(nameof(Order.Status));
// → [(key="paid", count=42), (key="pending", count=7), ...]

// Aggregations respeitam filtros do query.
var grandes = await repo.Query()
    .Where(nameof(Order.Total), ">", 100m)
    .CountAsync();
```

Em SQL: `SUM/AVG/MIN/MAX` no projection, `GROUP BY` explícito. Em Mongo: aggregate pipeline `$match` → `$group`.

---

## Scopes nomeados

```csharp
public class User : BaseModel<User, int>
{
    [Column("active")] public bool Active { get; set; }
    [Column("priority")] public int Priority { get; set; }

    [Scope("active")]
    public static IQuery<User, int> ScopeActive(IQuery<User, int> q) =>
        q.Where("Active", true);

    [Scope("highPriority")]
    public static IQuery<User, int> ScopeHighPriority(IQuery<User, int> q) =>
        q.Where("Priority", ">=", 5);
}

// Uso — encadeáveis:
var top = await repo.Scope("active").Scope("highPriority").Take(10).ToListAsync();
var topByName = await repo.Scope("active").OrderBy(nameof(User.Name)).ToListAsync();
```

Scope deve ter assinatura `IQuery<T, TId>(IQuery<T, TId>)`. Scopes desconhecidos lançam `ArgumentException` clara.

---

## Bulk update / delete

Operações em massa por filtro, sem hidratar entidades (rápido, mas **não dispara hooks de modelo**):

```csharp
var n = await repo.Query()
    .Where(nameof(User.Active), false)
    .UpdateAsync(new Dictionary<string, object>
    {
        [nameof(User.Active)] = true
    });

await repo.Query().Where(nameof(User.Banned), true).DeleteAsync();
```

Se o modelo tem `[SoftDelete]`, `DeleteAsync()` vira `UPDATE deleted_at` automaticamente.

---

## Hooks de ciclo de vida

Override em `BaseModel<T, TId>` para validar, normalizar, gerar valores derivados, log, side-effects:

```csharp
public class User : BaseModel<User, int>
{
    public string PlainPassword { get; set; }                    // não-coluna
    [Column("password_hash")] public string PasswordHash { get; set; }

    protected internal override void OnBeforeCreate()
    {
        if (!string.IsNullOrEmpty(PlainPassword))
            PasswordHash = HashPassword(PlainPassword);   // use seu hash favorito
    }
}
```

### Hooks disponíveis

| Hook | Quando |
|---|---|
| `OnBeforeSave(isInsert)` / `OnAfterSave(isInsert)` | Toda chamada Save (insert ou update). |
| `OnBeforeCreate` / `OnAfterCreate` | Apenas em insert. |
| `OnBeforeUpdate` / `OnAfterUpdate` | Apenas em update. |
| `OnBeforeValidate` / `OnAfterValidate` | Em volta de `ModelValidator.Validate`. |
| `OnBeforeDelete` / `OnAfterDelete` | Em volta do delete (ou soft-delete). |
| `OnBeforeRestore` / `OnAfterRestore` | Em volta de `RestoreAsync`. |
| `OnAfterLoad` | Após hidratar entidade do banco. |

Cada um tem versão síncrona e Async. Os Async dos `OnBefore*Create`/`Update`/`Delete` aceitam um `HookContext` opcional (ver abaixo).

### Cancelamento sem exceção

Override `CanSaveAsync`/`CanDeleteAsync` retornando `false` aborta a operação silenciosamente — útil para middleware (RBAC, feature flags, soft-mode):

```csharp
protected internal override Task<bool> CanSaveAsync(bool isInsert)
{
    if (Email != null && Email.EndsWith("@blocked.com"))
        return Task.FromResult(false);   // não persiste, não lança
    return Task.FromResult(true);
}
```

### Ordem do `SaveAsync`

```
OnBeforeSave  →  OnBeforeCreate|Update  →  CanSaveAsync (false aborta)
              →  OnBeforeValidate  →  ModelValidator  →  OnAfterValidate
              →  INSERT/UPDATE  →  Cascade save
              →  OnAfterCreate|Update  →  OnAfterSave
```

---

## Validação declarativa

```csharp
public class User : BaseModel<User, int>
{
    [Column("email")]
    [Required] [RegEx(@"^[^@]+@[^@]+$")]
    public string Email { get; set; }

    [Column("nickname", length: 50)]
    [MinLength(3)] [MaxLength(20)]
    public string Nickname { get; set; }

    [Column("age")] [Range(0, 150)]
    public int Age { get; set; }
}

try { await repo.SaveAsync(user); }
catch (ValidationException ex)
{
    foreach (var e in ex.Errors)
        Console.WriteLine($"{e.PropertyName}: {e.Message}");
}
```

Falhas viram `ValidationException` com lista de `ValidationError` agregando todos os erros de uma vez.

Atributos: `[Required]`, `[MaxLength]`, `[MinLength]`, `[RegEx]`, `[Range]`. Atributos customizados: herde `ValidationAttribute` e implemente `string Validate(object value)`.

---

## `HookContext` — side effects multi-tabela

Hooks `Async` recebem um `HookContext` opcional que dá acesso a outros repositórios **na mesma transação**. Audit log atômico fica trivial:

```csharp
public class Order : BaseModel<Order, int>
{
    [Column("total")] public decimal Total { get; set; }

    protected internal override async Task OnAfterUpdateAsync(HookContext ctx)
    {
        var auditRepo = ctx.GetRepository<AuditEntry, int>();
        await auditRepo.SaveAsync(new AuditEntry
        {
            EntityType = nameof(Order),
            EntityId = Id,
            Hash = ComputeHash(),
            ChangedAt = DateTime.UtcNow
        });
    }
}
```

Se a transação do save do `Order` for revertida (rollback, exceção, conflito de versão), o registro de audit também volta. **Atomicidade real.**

---

## Cascade save & delete

Opt-in via `cascade` / `cascadeDelete` em `[OneToOne]`/`[OneToMany]`. Tudo na mesma transação:

```csharp
public class Parent : BaseModel<Parent, int>
{
    [OneToMany("parent_id", typeof(Child), cascade: true, cascadeDelete: true)]
    public Child[] Children { get; set; }
}

var p = new Parent {
    Name = "p",
    Children = new[] { new Child { Label = "c1" }, new Child { Label = "c2" } }
};
await parents.SaveAsync(p);
// p.Id preenchido; cada Child.Id preenchido; Child.ParentId = p.Id; tudo na mesma tx.

await parents.DeleteAsync(p);
// Apaga (ou soft-deleta) os filhos primeiro, depois o pai.
```

`ManyToMany` não tem cascade automático ainda (issue #56).

---

## Eager loading multi-nível

`includeRelated: true` carrega navigation properties recursivamente até **3 níveis de profundidade** (default), com query única `WHERE pk IN (...)` por relacionamento (sem N+1).

```csharp
public class Grandparent : BaseModel<Grandparent, int>
{
    [OneToMany("grandparent_id", typeof(MidParent))]
    public MidParent[] Mids { get; set; }
}

public class MidParent : BaseModel<MidParent, int>
{
    [OneToMany("midparent_id", typeof(Leaf))]
    public Leaf[] Leaves { get; set; }
}

var g = await grandparents.FindByIdAsync(id, includeRelated: true);
// g.Mids preenchido; cada g.Mids[i].Leaves preenchido também.
```

---

## Soft delete

```csharp
[SoftDelete]                                    // default columnName = "deleted_at"
public class User : BaseModel<User, int> { }

await repo.DeleteAsync(user);                   // UPDATE deleted_at = now()
await repo.FindByIdAsync(id);                   // null (filtra deletados por default)
await repo.FindByIdIncludingDeletedAsync(id);   // achou
await repo.RestoreAsync(user);                  // deleted_at = NULL

await repo.FindAllIncludingDeletedAsync();
await repo.Query().Where(...).DeleteAsync();    // bulk soft-delete
```

`Query()` filtra `deleted_at IS NULL` automaticamente. Funciona em **SQL e Mongo**.

---

## Optimistic locking (`[Version]`)

```csharp
public class Order : BaseModel<Order, int>
{
    [Column("total")] public decimal Total { get; set; }
    [Column("row_version")] [Version] public int RowVersion { get; set; }
}

try { await repo.SaveAsync(order); }
catch (DbConcurrencyException)
{
    // outro processo modificou a linha desde que você leu
    var fresh = await repo.FindByIdAsync(order.Id);
    // ... tente de novo
}
```

Insert inicializa em `1`. Cada update incrementa **e** adiciona `AND row_version = @oldVersion` ao WHERE. Se nenhuma linha for atualizada, lança e o valor em memória é revertido para o original (permite reload + retry sem estado inconsistente).

Funciona em SQL e Mongo (Mongo usa `ReplaceOne` com filter `_id + version`).

---

## Embedded (Mongo)

```csharp
public class EmbeddedAddress
{
    [Column("street")] public string Street { get; set; }
    [Column("city")]   public string City { get; set; }
}

public class User : BaseModel<User, string>
{
    [Embedded] public EmbeddedAddress[] Addresses { get; set; }
    [Embedded] public EmbeddedAddress PrimaryAddress { get; set; }
}
```

Subdocumentos são POCO comum (não herdam `BaseModel`). Serializados como `BsonArray`/`BsonDocument` aninhado. Em SQL, `[Embedded]` é silenciosamente ignorado.

---

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

`SaveManyAsync`, `Query`, `Find...` e cascade respeitam a transação ambiente.

---

## Raw queries

Escape hatch para SQL bruto:

```csharp
var rows = await repo.RawAsync(
    "SELECT * FROM users WHERE created_at > @cutoff",
    new Dictionary<string, object> { ["cutoff"] = DateTime.UtcNow.AddDays(-7) });
```

Cada linha é materializada em `T` via `Populate` (mesmo caminho dos demais métodos). Hooks `OnAfterLoad` disparam normalmente. Respeita transação ambiente.

---

## Migrations

Sistema de migrations versionadas estilo Laravel — você escreve cada migration manualmente, o runner aplica em ordem e mantém histórico em `__lightorm_migrations`.

```csharp
public class M2026_05_09_120000_CreateUsers : Migration
{
    public override void Up(SchemaBuilder schema) =>
        schema.Create("users", t =>
        {
            t.Id();
            t.String("name", 100);
            t.String("email", 200).Unique();
            t.Bool("active").Default("0");
            t.Timestamps();           // CreatedAt + UpdatedAt
            // t.SoftDeletes();       // opcional: deleted_at nullable
        });

    public override void Down(SchemaBuilder schema) =>
        schema.DropIfExists("users");
}

public class M2026_05_09_130000_AddRoleToUsers : Migration
{
    public override void Up(SchemaBuilder schema) =>
        schema.Alter("users", t => t.AddString("role", 50, c => c.Nullable()));

    public override void Down(SchemaBuilder schema) =>
        schema.Alter("users", t => t.DropColumn("role"));
}
```

Aplicação:

```csharp
var runner = new MigrationRunner(connection, dialect);
var ran = await runner.MigrateAsync(typeof(M2026_05_09_120000_CreateUsers).Assembly);
// ran = ["M2026_05_09_120000_CreateUsers", "M2026_05_09_130000_AddRoleToUsers"]

await runner.RollbackAsync(assembly);     // desfaz a última
await runner.GetAppliedAsync();           // lista ordenada das aplicadas
```

### Métodos do `TableBuilder` (dentro de `Create`/`Alter`)

| Método | Tipo SQL gerado |
|---|---|
| `Id(name = "Id")` | PK auto-increment INT |
| `Int` / `Long` / `Short` | INT / BIGINT / SMALLINT |
| `String(name, length = 255)` | VARCHAR(length) (TEXT em SQLite) |
| `Bool` | BOOLEAN / TINYINT(1) / INTEGER |
| `DateTime` | DATETIME / TIMESTAMP / TEXT |
| `Decimal` | DECIMAL(18,2) / NUMERIC |
| `Float` / `Double` | FLOAT / REAL / DOUBLE PRECISION |
| `Guid` | CHAR(36) / UUID / TEXT |
| `Bytes` | BLOB / BYTEA |
| `Timestamps()` | adiciona `CreatedAt` e `UpdatedAt` |
| `SoftDeletes(name = "deleted_at")` | adiciona `deleted_at` nullable |

### Modificadores do `ColumnBuilder`

`.Nullable()`, `.Unique()`, `.Default("sql literal")`, `.Index(name?)`, `.Primary()`, `.AutoIncrement()`, `.Unsigned()`, `.References(refTable, refCol = "Id")`.

### `AlterTableBuilder`

`.AddColumn<T>(name, configure?)`, `.AddString(name, length, configure?)`, `.DropColumn(name)`, `.AddIndex(col, unique?, indexName?)`, `.DropIndex(name)`, `.Raw(sql)`.

### Histórico

`MigrationRunner` cria `__lightorm_migrations` (PRIMARY KEY = `name`, `applied_at` DateTime) automaticamente. Cada migration aplica em **transação própria**: falha aborta sem registrar.

Migrations descobertas via reflexão no assembly que você passa, ordenadas por nome (use prefixo timestamp `M20260509_120000_*` para ordem cronológica natural).

**Não inclui CLI** (você chama `MigrateAsync` no startup do app). Roadmap: issue #38.

---

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

Configure servidor/usuário/senha no Inspector do `DatabaseManager`. Helper específico para SQLite local (save de jogo) está na issue #49.

---

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

**295 testes verdes** atualmente. Cobertura inclui:

- Persistence: CRUD, transações, soft delete, optimistic locking, upsert, migrations.
- Querying: filtros, agregações, bulk update/delete, scopes, OR composto.
- Lifecycle: hooks granulares + cancelamento + `HookContext`.
- Relationships: cascade save/delete, eager multi-nível, edge cases.
- Providers: testes específicos por dialect (MySQL, SQLite, Postgres, Mongo).
- Security: SQL injection neutralizado.
- Infrastructure: validação de atributos, falhas em populate.

---

## Limitações conhecidas

| Limitação | Issue |
|---|---|
| Eager loading não filtra filhos com soft-delete. | [#34](https://github.com/MarcosBrendonDePaula/LightOrm/issues/34) |
| Restore não cascateia para filhos. | [#35](https://github.com/MarcosBrendonDePaula/LightOrm/issues/35) |
| `Query.Select<TDto>(...)` projeção tipada não existe. | [#36](https://github.com/MarcosBrendonDePaula/LightOrm/issues/36) |
| Mongo: `[OneToMany]`/`[ManyToMany]` via `$lookup` não implementado (use `[Embedded]`). | [#37](https://github.com/MarcosBrendonDePaula/LightOrm/issues/37) |
| Migrations sem CLI / sem auto-diff. | [#38](https://github.com/MarcosBrendonDePaula/LightOrm/issues/38) |
| Sem expression tree em `Where(u => u.Age > 18)` — só string-based. | [#40](https://github.com/MarcosBrendonDePaula/LightOrm/issues/40) |
| Bulk `Query.UpdateAsync`/`DeleteAsync` não dispara hooks de modelo. | [#41](https://github.com/MarcosBrendonDePaula/LightOrm/issues/41) |
| Mongo: `[Index]`/`[Unique]` não cria índices automaticamente. | [#42](https://github.com/MarcosBrendonDePaula/LightOrm/issues/42) |
| Sem `[Default(value)]` / `[CheckConstraint]`. | [#43](https://github.com/MarcosBrendonDePaula/LightOrm/issues/43) |
| `enum` não suportado no `MapType`. | [#44](https://github.com/MarcosBrendonDePaula/LightOrm/issues/44) |

Veja [todas as 25 issues abertas](https://github.com/MarcosBrendonDePaula/LightOrm/issues) para o roadmap completo.

---

## Licença

MIT.
