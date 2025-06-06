# Uso do LightOrm

O LightOrm foi projetado para ser intuitivo e fácil de usar. Esta seção irá guiá-lo através dos passos básicos para definir seus modelos, realizar operações CRUD (Criar, Ler, Atualizar, Excluir) e interagir com seu banco de dados MySQL.

## 1. Definindo Seus Modelos

Para usar o LightOrm, suas classes de modelo devem herdar de `BaseModel<T>` e usar atributos específicos para mapear propriedades para colunas do banco de dados. Cada modelo representa uma tabela no seu banco de dados.

### Exemplo de Modelo Básico

Considere uma tabela `users` com colunas `Id`, `user_name`, `email_address` e `is_active`. Seu modelo C# correspondente seria:

```csharp
using LightOrm.Core.Models;
using LightOrm.Core.Attributes;
using System;

public class User : BaseModel<User>
{
    // Define o nome da tabela no banco de dados
    public override string TableName => "users";

    // A propriedade Id é automaticamente mapeada como chave primária e auto-incremento
    // Você pode sobrescrever isso com um atributo Column se necessário
    // [Column("Id", isPrimaryKey: true, autoIncrement: true)]
    // public int Id { get; set; }

    [Column("user_name", length: 100)]
    public string UserName { get; set; }

    [Column("email_address", length: 255)]
    public string EmailAddress { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; }

    // CreatedAt e UpdatedAt são automaticamente gerenciados pelo BaseModel
    // [Column("CreatedAt")]
    // public DateTime CreatedAt { get; set; }

    // [Column("UpdatedAt")]
    // public DateTime UpdatedAt { get; set; }
}
```

### Atributos de Coluna

O LightOrm utiliza o `[Column]` atributo para configurar o mapeamento de propriedades:

*   `[Column(string name, bool isPrimaryKey = false, bool autoIncrement = false, int length = 0, bool canBeNull = true)]`
    *   `name`: O nome da coluna no banco de dados.
    *   `isPrimaryKey`: Define se a coluna é a chave primária da tabela (padrão: `false`). A propriedade `Id` em `BaseModel` já é configurada como chave primária e auto-incremento por padrão.
    *   `autoIncrement`: Define se a coluna é auto-incrementável (padrão: `false`).
    *   `length`: O comprimento máximo para colunas de texto (ex: `VARCHAR(length)`).
    *   `canBeNull`: Define se a coluna pode aceitar valores nulos (padrão: `true`).

## 2. Conectando ao Banco de Dados

Antes de realizar qualquer operação, você precisa de uma conexão ativa com o banco de dados MySQL. O LightOrm utiliza `MySqlConnection` do `MySql.Data`.

```csharp
using MySql.Data.MySqlClient;
using System.Threading.Tasks;

public class DatabaseManager
{
    private readonly string _connectionString = "Server=localhost;Port=3307;Database=your_database;Uid=root;Pwd=my-secret-pw;";

    public async Task<MySqlConnection> GetConnectionAsync()
    {
        var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();
        return connection;
    }
}
```

## 3. Operações CRUD

O LightOrm fornece métodos assíncronos para as operações CRUD básicas através da classe `BaseModel`.

### Criar Tabela (EnsureTableExistsAsync)

Antes de inserir dados, você pode garantir que a tabela correspondente ao seu modelo exista no banco de dados. Se a tabela não existir, ela será criada com base na definição do seu modelo.

```csharp
using (var connection = await new DatabaseManager().GetConnectionAsync())
{
    var userModel = new User();
    await userModel.EnsureTableExistsAsync(connection);
    Console.WriteLine("Tabela 'users' verificada/criada com sucesso.");
}
```

### Inserir Dados (SaveAsync)

Para inserir um novo registro, crie uma instância do seu modelo e chame `SaveAsync()`. Se o `Id` do objeto for 0 (padrão para novos objetos), uma operação de inserção será realizada.

```csharp
using (var connection = await new DatabaseManager().GetConnectionAsync())
{
    var newUser = new User
    {
        UserName = "Alice Smith",
        EmailAddress = "alice@example.com",
        IsActive = true
    };

    await newUser.SaveAsync(connection);
    Console.WriteLine($"Novo usuário inserido com ID: {newUser.Id}");
}
```

### Ler Dados (FindByIdAsync, FindAllAsync)

Você pode recuperar registros por ID ou todos os registros de uma tabela.

*   **FindByIdAsync(connection, id)**: Recupera um único registro pelo seu ID.

    ```csharp
    using (var connection = await new DatabaseManager().GetConnectionAsync())
    {
        var user = await User.FindByIdAsync(connection, 1);
        if (user != null)
        {
            Console.WriteLine($"Usuário encontrado: {user.UserName} ({user.EmailAddress})");
        }
        else
        {
            Console.WriteLine("Usuário não encontrado.");
        }
    }
    ```

*   **FindAllAsync(connection)**: Recupera todos os registros de uma tabela.

    ```csharp
    using (var connection = await new DatabaseManager().GetConnectionAsync())
    {
        var allUsers = await User.FindAllAsync(connection);
        Console.WriteLine("Todos os usuários:");
        foreach (var user in allUsers)
        {
            Console.WriteLine($"- {user.UserName} ({user.EmailAddress})");
        }
    }
    ```

### Atualizar Dados (SaveAsync)

Para atualizar um registro existente, modifique as propriedades de uma instância do modelo que já possui um `Id` (ou seja, foi carregada do banco de dados ou teve seu `Id` definido manualmente) e chame `SaveAsync()`. Uma operação de atualização será realizada.

```csharp
using (var connection = await new DatabaseManager().GetConnectionAsync())
{
    var userToUpdate = await User.FindByIdAsync(connection, 1);
    if (userToUpdate != null)
    {
        userToUpdate.UserName = "Alicia Wonderland";
        userToUpdate.IsActive = false;
        await userToUpdate.SaveAsync(connection);
        Console.WriteLine($"Usuário {userToUpdate.Id} atualizado para: {userToUpdate.UserName}");
    }
}
```

### Excluir Dados (DeleteAsync)

Para excluir um registro, chame `DeleteAsync()` em uma instância do modelo que você deseja remover.

```csharp
using (var connection = await new DatabaseManager().GetConnectionAsync())
{
    var userToDelete = await User.FindByIdAsync(connection, 1);
    if (userToDelete != null)
    {
        await userToDelete.DeleteAsync(connection);
        Console.WriteLine($"Usuário {userToDelete.Id} excluído com sucesso.");
    }
}
```

Esta seção cobriu as operações básicas do LightOrm. Na próxima seção, exploraremos como definir e gerenciar relacionamentos entre seus modelos.

