# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Projects in the solution

`LightOrm.sln` contains four C# projects, but only three are referenced by the solution file (`LightOrm.Core.Tests` is built/run separately):

- **LightOrm.Core** — the ORM library itself. `netstandard2.1`, `LangVersion 8.0`, nullable disabled, implicit usings disabled. Depends on `MySql.Data` 8.0.33. This constrained target (netstandard2.1 / C# 8) is intentional so the same DLL works in Unity — do not raise it.
- **LightOrm.Unity** — Unity-specific glue (`DatabaseManager` MonoBehaviour). Same target/lang constraints as Core. References `UnityEngine.dll` from a hard-coded path (`C:\Program Files\Unity\Hub\Editor\2022.3.4f1\...`); a `PostBuild` target copies the output DLL to `..\Assets\Plugins`. Building this project requires that Unity install on the machine.
- **LightOrm.Tests** — `net8.0` console app (`Program.cs` + `RelationshipTests.cs`). This is a manual/integration runner, not xUnit.
- **LightOrm.Core.Tests** — `net8.0` xUnit test project (the real automated test suite). Not in the .sln; build/test by path.

## Build / test commands

```
dotnet build LightOrm.sln                       # builds Core + Unity + Tests (will fail without Unity installed)
dotnet build LightOrm.Core/LightOrm.Core.csproj # build just the library
dotnet test  LightOrm.Core.Tests/LightOrm.Core.Tests.csproj
dotnet test  LightOrm.Core.Tests/LightOrm.Core.Tests.csproj --filter "FullyQualifiedName~CrudTests"
dotnet run   --project LightOrm.Tests           # manual relationship/CRUD runner
```

Tests require a local MySQL at `localhost:3307`, root user, password `my-secret-pw` (see `LightOrm.Core.Tests/TestBase.cs`). Each test class spins up a uniquely-named database (`testdb_<guid>`) in `InitializeAsync` and drops it in `DisposeAsync`, so tests are isolated but cannot run without that server available.

## Architecture

Single-file core: nearly all ORM behavior lives in `LightOrm.Core/Models/BaseModel.cs`. Models inherit `BaseModel<T>` (CRTP) and gain `Id`, `CreatedAt`, `UpdatedAt`, plus instance methods `EnsureTableExistsAsync`, `SaveAsync` (insert+update by `Id`), `DeleteAsync`, and statics `FindByIdAsync`, `FindAllAsync`. `TableName` is abstract.

Schema and relationships are declared via attributes in `LightOrm.Core/Attributes/`: `ColumnAttribute` (name, length, isPrimaryKey, autoIncrement, isUnsigned, etc.), `ForeignKeyAttribute`, `OneToOneAttribute`, `OneToManyAttribute`, `ManyToManyAttribute`. `EnsureTableExistsAsync` reflects over these to emit `CREATE TABLE IF NOT EXISTS`; `FindByIdAsync(..., includeRelated: true)` walks the relationship attributes to issue follow-up queries (`LoadRelatedEntityAsync` / `LoadRelatedDataAsync` / `LoadManyToManyRelationshipsAsync`).

Reflection performance matters because every CRUD call touches a model's `PropertyInfo` set. `LightOrm.Core/Utilities/TypeMetadataCache.cs` is the single cache for properties and resolved attributes — go through it rather than calling `GetProperties` / `GetCustomAttribute` directly. SQL is built with parameterized commands; the existing `SqlInjectionTests.cs` is the regression guard for this — keep all user-supplied values flowing through `MySqlParameter`, never string interpolation into SQL.

Connection management is split: standard apps use `LightOrm.Core/Database/DatabaseConnection.cs` directly with `MySqlConnection`; Unity apps use the singleton `LightOrm.Unity/Database/DatabaseManager.cs` MonoBehaviour configured in the Inspector. Both ultimately produce a `MySqlConnection` that gets passed into `BaseModel<T>` methods — the model layer is connection-agnostic.

## Documentation site

`docs/` is a Jekyll site (Cayman theme) published via GitHub Pages. Edits to user-facing docs go there, not in README.md alone.
