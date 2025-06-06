# LightOrm - Documentação

## Introdução

Bem-vindo à documentação do LightOrm, um ORM (Object-Relational Mapper) leve e eficiente, projetado para simplificar a interação com bancos de dados MySQL em projetos C#, com especial atenção para a integração com o Unity. O LightOrm visa fornecer uma camada de abstração que permite aos desenvolvedores manipular dados como objetos C#, eliminando a necessidade de escrever SQL complexo e repetitivo.

### O que é um ORM?

Um ORM é uma técnica de programação que mapeia um sistema de tipo de objeto para um sistema de banco de dados relacional. Isso permite que os desenvolvedores trabalhem com objetos em sua linguagem de programação preferida, enquanto o ORM se encarrega de traduzir essas operações para consultas SQL e vice-versa. Os benefícios incluem:

*   **Produtividade Aumentada**: Reduz a quantidade de código boilerplate necessário para interagir com o banco de dados.
*   **Manutenibilidade**: O código se torna mais limpo e fácil de entender, pois as operações de banco de dados são representadas de forma orientada a objetos.
*   **Portabilidade**: Facilita a mudança entre diferentes sistemas de banco de dados (embora o LightOrm atualmente foque em MySQL, a arquitetura permite extensões futuras).
*   **Segurança**: Ajuda a prevenir vulnerabilidades comuns, como SQL Injection, ao parametrizar consultas.

### Por que LightOrm?

O LightOrm foi desenvolvido com a simplicidade e a performance em mente. Diferente de ORMs mais robustos e complexos, o LightOrm oferece uma abordagem mais direta para o mapeamento objeto-relacional, tornando-o ideal para projetos onde a agilidade e a leveza são cruciais. Suas principais características incluem:

*   **Mapeamento Simples**: Utiliza atributos C# para definir o mapeamento entre classes e tabelas do banco de dados, e propriedades e colunas.
*   **Operações CRUD Simplificadas**: Métodos intuitivos para criar, ler, atualizar e excluir registros.
*   **Foco em Performance**: Implementa cache de reflexão para minimizar o overhead e otimizar o acesso a metadados.
*   **Segurança Reforçada**: Inclui mecanismos para prevenir ataques de SQL Injection, garantindo que os dados estejam protegidos.
*   **Compatibilidade com Unity**: Projetado para funcionar harmoniosamente em ambientes Unity, permitindo que desenvolvedores de jogos e aplicações C# aproveitem os benefícios de um ORM leve.

Esta documentação irá guiá-lo através da instalação, configuração e uso do LightOrm, além de fornecer exemplos práticos e detalhes sobre sua arquitetura e funcionalidades. 

