# Arquitetura e Melhorias do LightOrm

Esta seção aprofunda-se na arquitetura interna do LightOrm e nas melhorias recentes que foram implementadas para otimizar a performance e a segurança da biblioteca.

## Visão Geral da Arquitetura

O LightOrm é construído em torno de um design simples e direto, focado em fornecer uma camada de abstração leve sobre o MySQL. Os componentes chave incluem:

*   **`BaseModel<T>`**: A classe abstrata fundamental que serve como base para todos os modelos de dados. Ela encapsula a lógica CRUD genérica, o mapeamento de propriedades para colunas do banco de dados e o gerenciamento de relacionamentos.
*   **Atributos de Mapeamento**: Atributos C# como `[Column]`, `[ForeignKey]`, `[OneToOne]`, `[OneToMany]` e `[ManyToMany]` são usados para definir o esquema do banco de dados e os relacionamentos diretamente nas classes de modelo, promovendo uma abordagem de "convenção sobre configuração" com flexibilidade para personalização.
*   **`MySqlConnection`**: O LightOrm utiliza diretamente a classe `MySqlConnection` do pacote `MySql.Data` para interagir com o banco de dados MySQL, garantindo compatibilidade e desempenho.
*   **Geração Dinâmica de SQL**: Consultas SQL para operações CRUD e criação de tabelas são geradas dinamicamente em tempo de execução com base nos metadados dos modelos.

## Melhorias Implementadas

As melhorias recentes focaram em dois pilares principais: **Performance** e **Segurança**.

### 1. Otimização de Performance: Cache de Reflexão

**Problema Original**: O uso extensivo da API de Reflexão do .NET para inspecionar tipos, propriedades e atributos em cada operação de banco de dados resultava em um overhead de performance considerável. A reflexão é uma ferramenta poderosa, mas seu custo de execução pode ser alto, especialmente em cenários de alta frequência de operações.

**Solução Implementada**: Introdução da classe `TypeMetadataCache` (`LightOrm.Core/Utilities/TypeMetadataCache.cs`). Esta classe atua como um cache centralizado para armazenar metadados de tipo e atributos. Quando um tipo é acessado pela primeira vez, suas informações relevantes (propriedades, atributos `[Column]`, `[ForeignKey]`, etc.) são extraídas via reflexão e armazenadas em `ConcurrentDictionary`s. Em acessos subsequentes, os metadados são recuperados diretamente do cache, eliminando a necessidade de re-executar operações de reflexão caras.

**Benefícios**:
*   **Redução do Overhead**: Diminui drasticamente o tempo de execução para operações de mapeamento e geração de SQL.
*   **Melhora na Responsividade**: Contribui para uma biblioteca mais rápida e responsiva, crucial para aplicações com requisitos de alta performance, como jogos.
*   **Thread-Safe**: O uso de `ConcurrentDictionary` garante que o cache seja seguro para uso em ambientes multi-threaded, prevenindo condições de corrida e garantindo a consistência dos dados em cache.

### 2. Aprimoramento de Segurança: Prevenção de SQL Injection

**Problema Original**: Embora o LightOrm utilizasse parâmetros para a maioria das operações de dados (o que é uma prática recomendada contra SQL Injection), a geração dinâmica de SQL para a criação de tabelas (`EnsureTableExistsAsync`) e a inclusão de nomes de tabelas/colunas diretamente nas consultas SQL apresentavam um vetor potencial para ataques de SQL Injection se os nomes não fossem devidamente tratados.

**Solução Implementada**: Todos os identificadores de tabela e coluna gerados dinamicamente agora são explicitamente escapados usando backticks (`` ` ``) no MySQL. Por exemplo, um nome de tabela `my_table` se torna `` `my_table` `` na consulta SQL. Isso garante que qualquer caractere especial ou malicioso presente em um nome de tabela ou coluna seja tratado como parte do identificador e não como um comando SQL executável.

**Benefícios**:
*   **Proteção Contra Injeção**: Impede que entradas maliciosas em nomes de tabelas ou colunas sejam interpretadas como comandos SQL, protegendo a integridade do banco de dados.
*   **Robustez**: Torna a biblioteca mais robusta contra entradas inesperadas ou maliciosas, aumentando a segurança geral da aplicação.

## Testes Unitários e Validação

Para validar a eficácia dessas melhorias, um novo conjunto de testes unitários foi desenvolvido (`LightOrm.Core.Tests`). Estes testes são executados em um ambiente isolado, utilizando um container Docker com MySQL 8.0, garantindo consistência e reprodutibilidade. Os testes incluem:

*   **Testes CRUD**: Validação das operações básicas de criação, leitura, atualização e exclusão de dados.
*   **Testes de SQL Injection**: Testes específicos que tentam explorar as vulnerabilidades de injeção de SQL em nomes de tabelas e colunas. A passagem desses testes confirma que as medidas de segurança implementadas são eficazes, resultando em exceções controladas ou tratamento literal dos dados maliciosos, em vez de execução de comandos indesejados.

**Resultado**: Todos os testes unitários foram aprovados, confirmando que as melhorias de performance e segurança estão funcionando conforme o esperado e que a biblioteca é mais robusta e eficiente.

## Considerações para o Unity

Embora as melhorias tenham sido implementadas na camada `LightOrm.Core`, que é agnóstica ao Unity, o impacto da reflexão no ambiente Unity (especialmente com a compilação IL2CPP) é uma consideração importante. O cache de reflexão implementado reduzirá significativamente o número de chamadas de reflexão em tempo de execução, o que é benéfico para a performance em plataformas onde a reflexão pode ser mais lenta. Futuras otimizações podem incluir a exploração de geradores de código-fonte (Source Generators) para eliminar completamente a necessidade de reflexão em tempo de execução para o mapeamento de modelos.

Esta seção fornece uma visão aprofundada das melhorias e da arquitetura do LightOrm. Para começar a usar a biblioteca, consulte a seção de [Instalação](installation.md) e [Uso](usage.md).

