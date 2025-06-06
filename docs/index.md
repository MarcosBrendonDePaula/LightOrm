---
layout: default
title: LightOrm - Um ORM leve e eficiente para C# e Unity
---

<div class="hero">
    <h1>LightOrm</h1>
    <p>Um ORM (Object-Relational Mapper) leve e eficiente, projetado para simplificar a interação com bancos de dados MySQL em projetos C#, com especial atenção para a integração com o Unity.</p>
    <div class="buttons">
        <a href="{{ 
'/installation' | relative_url }}" class="button">Começar</a>
        <a href="https://github.com/MarcosBrendonDePaula/LightOrm" class="button secondary" target="_blank">Ver no GitHub</a>
    </div>
</div>

## Guia Rápido para Desenvolvedores

O LightOrm foi criado para acelerar seu desenvolvimento, permitindo que você interaja com bancos de dados MySQL de forma intuitiva e orientada a objetos. Aqui estão os principais recursos e guias para você começar rapidamente:

<div class="feature-grid">
    <div class="feature-card">
        <h3><a href="{{ 
'/installation' | relative_url }}">Instalação</a></h3>
        <p>Aprenda como configurar o LightOrm em seu projeto C# ou Unity, incluindo pré-requisitos e opções de instalação via NuGet ou manual.</p>
    </div>
    <div class="feature-card">
        <h3><a href="{{ 
'/usage' | relative_url }}">Uso Básico (CRUD)</a></h3>
        <p>Domine as operações fundamentais de Criar, Ler, Atualizar e Excluir (CRUD) dados, e como definir seus modelos de forma eficiente.</p>
    </div>
    <div class="feature-card">
        <h3><a href="{{ 
'/relationships' | relative_url }}">Relacionamentos</a></h3>
        <p>Entenda como mapear e gerenciar relacionamentos complexos entre suas entidades, como Um-para-Um, Um-para-Muitos e Muitos-para-Muitos.</p>
    </div>
    <div class="feature-card">
        <h3><a href="{{ 
'/architecture' | relative_url }}">Arquitetura e Melhorias</a></h3>
        <p>Explore a arquitetura interna do LightOrm, as otimizações de performance (cache de reflexão) e as medidas de segurança contra SQL Injection.</p>
    </div>
</div>

## O que é um ORM?

Um ORM é uma técnica de programação que mapeia um sistema de tipo de objeto para um sistema de banco de dados relacional. Isso permite que os desenvolvedores trabalhem com objetos em sua linguagem de programação preferida, enquanto o ORM se encarrega de traduzir essas operações para consultas SQL e vice-versa. Os benefícios incluem:

<div class="feature-grid">
    <div class="feature-card">
        <h3>Produtividade Aumentada</h3>
        <p>Reduz a quantidade de código boilerplate necessário para interagir com o banco de dados.</p>
    </div>
    <div class="feature-card">
        <h3>Manutenibilidade</h3>
        <p>O código se torna mais limpo e fácil de entender, pois as operações de banco de dados são representadas de forma orientada a objetos.</p>
    </div>
    <div class="feature-card">
        <h3>Portabilidade</h3>
        <p>Facilita a mudança entre diferentes sistemas de banco de dados (embora o LightOrm atualmente foque em MySQL, a arquitetura permite extensões futuras).</p>
    </div>
    <div class="feature-card">
        <h3>Segurança</h3>
        <p>Ajuda a prevenir vulnerabilidades comuns, como SQL Injection, ao parametrizar consultas.</p>
    </div>
</div>

## Por que LightOrm?

O LightOrm foi desenvolvido com a simplicidade e a performance em mente. Diferente de ORMs mais robustos e complexos, o LightOrm oferece uma abordagem mais direta para o mapeamento objeto-relacional, tornando-o ideal para projetos onde a agilidade e a leveza são cruciais. Suas principais características incluem:

<div class="feature-grid">
    <div class="feature-card">
        <h3>Mapeamento Simples</h3>
        <p>Utiliza atributos C# para definir o mapeamento entre classes e tabelas do banco de dados, e propriedades e colunas.</p>
    </div>
    <div class="feature-card">
        <h3>Operações CRUD Simplificadas</h3>
        <p>Métodos intuitivos para criar, ler, atualizar e excluir registros.</p>
    </div>
    <div class="feature-card">
        <h3>Foco em Performance</h3>
        <p>Implementa cache de reflexão para minimizar o overhead e otimizar o acesso a metadados.</p>
    </div>
    <div class="feature-card">
        <h3>Segurança Reforçada</h3>
        <p>Inclui mecanismos para prevenir ataques de SQL Injection, garantindo que os dados estejam protegidos.</p>
    </div>
    <div class="feature-card">
        <h3>Compatibilidade com Unity</h3>
        <p>Projetado para funcionar harmoniosamente em ambientes Unity, permitindo que desenvolvedores de jogos e aplicações C# aproveitem os benefícios de um ORM leve.</p>
    </div>
</div>

## Exemplo Rápido

Veja como é simples definir um modelo e realizar operações básicas com o LightOrm:

```csharp
// Definindo um modelo
public class User : BaseModel<User>
{
    public override string TableName => "users";

    [Column("user_name", length: 100)]
    public string UserName { get; set; }

    [Column("email_address", length: 255)]
    public string EmailAddress { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; }
}

// Usando o modelo
using (var connection = await new DatabaseManager().GetConnectionAsync())
{
    // Criar tabela se não existir
    var userModel = new User();
    await userModel.EnsureTableExistsAsync(connection);

    // Inserir um novo usuário
    var newUser = new User
    {
        UserName = "Alice Smith",
        EmailAddress = "alice@example.com",
        IsActive = true
    };
    await newUser.SaveAsync(connection);

    // Buscar todos os usuários
    var allUsers = await User.FindAllAsync(connection);
    foreach (var user in allUsers)
    {
        Console.WriteLine($"- {user.UserName} ({user.EmailAddress})");
    }
}
```


