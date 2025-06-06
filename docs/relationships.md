# Relacionamentos no LightOrm

O LightOrm oferece suporte para os tipos mais comuns de relacionamentos entre entidades de banco de dados: Um-para-Um (One-to-One), Um-para-Muitos (One-to-Many) e Muitos-para-Muitos (Many-to-Many). Estes relacionamentos são definidos usando atributos específicos nas propriedades de navegação dos seus modelos.

## 1. Relacionamento Um-para-Um (One-to-One)

Um relacionamento Um-para-Um ocorre quando uma instância de uma entidade está associada a exatamente uma instância de outra entidade. No LightOrm, isso é geralmente modelado com uma chave estrangeira na tabela 


que contém a chave estrangeira para a outra tabela.

### Exemplo: Usuário e Perfil do Usuário

Considere que cada `User` tem um `UserProfile` único. A tabela `user_profiles` conteria uma chave estrangeira para a tabela `users`.

```csharp
using LightOrm.Core.Models;
using LightOrm.Core.Attributes;
using System;

public class UserProfile : BaseModel<UserProfile>
{
    public override string TableName => "user_profiles";

    [Column("user_id")]
    [ForeignKey("users")] // Indica que user_id é uma chave estrangeira para a tabela 'users'
    public int UserId { get; set; }

    [Column("bio", length: 500)]
    public string Bio { get; set; }

    [Column("website", length: 255)]
    public string Website { get; set; }
}

public class User : BaseModel<User>
{
    public override string TableName => "users";

    [Column("user_name", length: 100)]
    public string UserName { get; set; }

    [Column("email_address", length: 255)]
    public string EmailAddress { get; set; }

    // Propriedade de navegação para o relacionamento Um-para-Um
    // O primeiro parâmetro é o nome da propriedade de chave estrangeira neste modelo (UserId)
    // O segundo parâmetro é o tipo do modelo relacionado (UserProfile)
    [OneToOne("UserId", typeof(UserProfile))]
    public UserProfile Profile { get; set; }
}
```

### Atributos de Relacionamento Um-para-Um

*   `[ForeignKey(string referenceTable, string referenceColumn = "Id")]`
    *   Usado na propriedade que representa a chave estrangeira no modelo. Indica a tabela referenciada.
    *   `referenceTable`: O nome da tabela para a qual a chave estrangeira aponta.
    *   `referenceColumn`: O nome da coluna na tabela referenciada (padrão: `Id`).

*   `[OneToOne(string foreignKeyProperty, Type relatedType)]`
    *   Usado na propriedade de navegação (o objeto relacionado) no modelo.
    *   `foreignKeyProperty`: O nome da propriedade no *modelo atual* que atua como chave estrangeira para o `relatedType`.
    *   `relatedType`: O tipo da classe do modelo relacionado.

## 2. Relacionamento Um-para-Muitos (One-to-Many)

Um relacionamento Um-para-Muitos ocorre quando uma instância de uma entidade pode estar associada a várias instâncias de outra entidade, mas cada instância da segunda entidade está associada a apenas uma instância da primeira. Isso é tipicamente modelado com uma chave estrangeira na tabela 


do lado "muitos" que referencia a chave primária da tabela do lado "um".

### Exemplo: Usuário e Posts

Um `User` pode ter muitos `Post`s, mas cada `Post` pertence a apenas um `User`.

```csharp
using LightOrm.Core.Models;
using LightOrm.Core.Attributes;
using System;
using System.Collections.Generic;

public class Post : BaseModel<Post>
{
    public override string TableName => "posts";

    [Column("title", length: 200)]
    public string Title { get; set; }

    [Column("content", length: 1000)]
    public string Content { get; set; }

    [Column("user_id")]
    [ForeignKey("users")] // Chave estrangeira para a tabela de usuários
    public int UserId { get; set; }

    // Propriedade de navegação para o relacionamento Um-para-Um (o lado "um" do relacionamento)
    [OneToOne("UserId", typeof(User))]
    public User Author { get; set; }
}

public class User : BaseModel<User>
{
    public override string TableName => "users";

    [Column("user_name", length: 100)]
    public string UserName { get; set; }

    [Column("email_address", length: 255)]
    public string EmailAddress { get; set; }

    // Propriedade de navegação para o relacionamento Um-para-Muitos
    // O primeiro parâmetro é o nome da propriedade de chave estrangeira no modelo relacionado (Post.UserId)
    // O segundo parâmetro é o tipo do modelo relacionado (Post)
    [OneToMany("UserId", typeof(Post))]
    public List<Post> Posts { get; set; }
}
```

### Atributos de Relacionamento Um-para-Muitos

*   `[OneToMany(string foreignKeyProperty, Type relatedType)]`
    *   Usado na propriedade de navegação (a coleção de objetos relacionados) no modelo do lado "um".
    *   `foreignKeyProperty`: O nome da propriedade no *modelo relacionado* que atua como chave estrangeira para o modelo atual.
    *   `relatedType`: O tipo da classe do modelo relacionado (o lado "muitos").

## 3. Relacionamento Muitos-para-Muitos (Many-to-Many)

Um relacionamento Muitos-para-Muitos ocorre quando várias instâncias de uma entidade podem estar associadas a várias instâncias de outra entidade. Este tipo de relacionamento é geralmente implementado através de uma tabela de associação (ou tabela pivô) que contém chaves estrangeiras para ambas as tabelas principais.

### Exemplo: Posts e Tags

Um `Post` pode ter muitas `Tag`s, e uma `Tag` pode ser aplicada a muitos `Post`s.

```csharp
using LightOrm.Core.Models;
using LightOrm.Core.Attributes;
using System;
using System.Collections.Generic;

public class Tag : BaseModel<Tag>
{
    public override string TableName => "tags";

    [Column("name", length: 50)]
    public string Name { get; set; }
}

// Tabela de associação (pivô)
public class PostTag : BaseModel<PostTag>
{
    public override string TableName => "post_tags";

    [Column("post_id")]
    [ForeignKey("posts")]
    public int PostId { get; set; }

    [Column("tag_id")]
    [ForeignKey("tags")]
    public int TagId { get; set; }
}

public class Post : BaseModel<Post>
{
    public override string TableName => "posts";

    [Column("title", length: 200)]
    public string Title { get; set; }

    [Column("content", length: 1000)]
    public string Content { get; set; }

    [Column("user_id")]
    [ForeignKey("users")]
    public int UserId { get; set; }

    // Propriedade de navegação para o relacionamento Muitos-para-Muitos
    // O primeiro parâmetro é o nome da tabela de associação (PostTag.TableName)
    // O segundo parâmetro é a chave estrangeira deste modelo na tabela de associação (PostTag.PostId)
    // O terceiro parâmetro é a chave estrangeira do modelo relacionado na tabela de associação (PostTag.TagId)
    // O quarto parâmetro é o tipo do modelo relacionado (Tag)
    [ManyToMany("post_tags", "PostId", "TagId", typeof(Tag))]
    public List<Tag> Tags { get; set; }
}
```

### Atributos de Relacionamento Muitos-para-Muitos

*   `[ManyToMany(string associationTable, string sourceForeignKey, string targetForeignKey, Type relatedType)]`
    *   Usado na propriedade de navegação (a coleção de objetos relacionados) em ambos os modelos principais.
    *   `associationTable`: O nome da tabela de associação (pivô) que conecta as duas entidades.
    *   `sourceForeignKey`: O nome da coluna na tabela de associação que referencia a chave primária do *modelo atual*.
    *   `targetForeignKey`: O nome da coluna na tabela de associação que referencia a chave primária do *modelo relacionado*.
    *   `relatedType`: O tipo da classe do modelo relacionado.

## Carregando Dados Relacionados

Após definir os relacionamentos em seus modelos, o LightOrm pode carregar automaticamente os dados relacionados quando você recupera uma entidade principal. Isso é conhecido como *eager loading*.

Para carregar os dados relacionados, use o método `FindByIdAsync` ou `FindAllAsync` com o parâmetro `loadRelated` definido como `true`.

```csharp
using (var connection = await new DatabaseManager().GetConnectionAsync())
{
    // Carregar um usuário e seus posts relacionados
    var userWithPosts = await User.FindByIdAsync(connection, 1, loadRelated: true);
    if (userWithPosts != null)
    {
        Console.WriteLine($"Usuário: {userWithPosts.UserName}");
        if (userWithPosts.Posts != null && userWithPosts.Posts.Any())
        {
            Console.WriteLine("Posts:");
            foreach (var post in userWithPosts.Posts)
            {
                Console.WriteLine($"- {post.Title}");
            }
        }
        else
        {
            Console.WriteLine("Nenhum post encontrado para este usuário.");
        }

        // Carregar um post e seu autor (relacionamento One-to-One)
        var postWithAuthor = await Post.FindByIdAsync(connection, 1, loadRelated: true);
        if (postWithAuthor != null && postWithAuthor.Author != null)
        {
            Console.WriteLine($"\nPost: {postWithAuthor.Title}");
            Console.WriteLine($"Autor: {postWithAuthor.Author.UserName}");
        }

        // Carregar um post e suas tags (relacionamento Many-to-Many)
        var postWithTags = await Post.FindByIdAsync(connection, 1, loadRelated: true);
        if (postWithTags != null && postWithTags.Tags != null && postWithTags.Tags.Any())
        {
            Console.WriteLine($"\nPost: {postWithTags.Title}");
            Console.WriteLine("Tags:");
            foreach (var tag in postWithTags.Tags)
            {
                Console.WriteLine($"- {tag.Name}");
            }
        }
    }
}
```

**Observação**: O `BaseModel` do LightOrm tenta carregar os relacionamentos definidos pelos atributos `[OneToOne]`, `[OneToMany]` e `[ManyToMany]` quando `loadRelated` é `true`. Para relacionamentos complexos ou para otimizar o carregamento, você pode precisar de consultas mais específicas ou implementar lógicas de carregamento personalizadas.

Esta seção conclui a documentação sobre o uso e os relacionamentos no LightOrm. Na próxima seção, abordaremos tópicos mais avançados e a arquitetura interna da biblioteca.

