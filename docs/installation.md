# Instalação

Para utilizar o LightOrm em seu projeto C# ou Unity, siga os passos abaixo:

## Pré-requisitos

Certifique-se de ter os seguintes pré-requisitos instalados:

*   **SDK do .NET 8.0 ou superior**: O LightOrm é desenvolvido com base no .NET 8.0. Você pode baixá-lo em [https://dotnet.microsoft.com/download](https://dotnet.microsoft.com/download).
*   **MySQL Server**: O LightOrm interage com bancos de dados MySQL. Certifique-se de ter uma instância do MySQL Server acessível. Você pode instalar o MySQL diretamente ou usar Docker para uma configuração mais isolada (conforme utilizado nos testes da biblioteca).

## Instalação via NuGet (Recomendado)

A maneira mais fácil de adicionar o LightOrm ao seu projeto é através do pacote NuGet. Abra o Console do Gerenciador de Pacotes no Visual Studio ou use a CLI do .NET:

### Usando o Console do Gerenciador de Pacotes (Visual Studio)

```powershell
Install-Package LightOrm.Core
Install-Package MySql.Data
```

### Usando a CLI do .NET

Navegue até o diretório do seu projeto no terminal e execute os seguintes comandos:

```bash
dotnet add package LightOrm.Core
dotnet add package MySql.Data
```

O pacote `MySql.Data` é o conector oficial do MySQL para .NET e é necessário para que o LightOrm se comunique com o banco de dados.

## Instalação Manual (para desenvolvimento ou contribuição)

Se você pretende contribuir com o LightOrm ou deseja usar o código-fonte diretamente, siga estes passos:

1.  **Clone o Repositório**: Clone o repositório do LightOrm para sua máquina local:

    ```bash
    git clone https://github.com/MarcosBrendonDePaula/LightOrm.git
    ```

2.  **Abra no Visual Studio (ou IDE de sua preferência)**: Abra a solução `LightOrm.sln` no Visual Studio ou em sua IDE C# preferida.

3.  **Adicione Referência ao Projeto**: Em seu projeto, adicione uma referência ao projeto `LightOrm.Core`.

    *   No Visual Studio, clique com o botão direito no seu projeto no Gerenciador de Soluções, selecione `Adicionar` > `Referência de Projeto`, e marque `LightOrm.Core`.

4.  **Instale o MySql.Data**: Instale o pacote `MySql.Data` via NuGet em seu projeto, conforme as instruções acima.

Após a instalação, você estará pronto para configurar e utilizar o LightOrm em seu projeto. Continue para a próxima seção para aprender como configurar sua conexão com o banco de dados e começar a mapear seus modelos.

