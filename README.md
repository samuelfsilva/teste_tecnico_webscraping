# LegalScraper - Teste Técnico (Web Scraping + API)

Esta é a solução para o desafio técnico de extração de dados jurídicos (TJSP e TRTs do PJe).

## Tecnologias Utilizadas

- **.NET 8** (ou superior) para API e Background Scraping
- **Entity Framework Core (SQLite)** para persistência dos dados (fácil execução sem dependência de Docker/SQL Server externo)
- **Microsoft Playwright** para automação web (Scraping headless/headful e tratamento de desafios Javascript)
- **MediatR** para CQRS (Comandos e Queries isolados)

## Estrutura do Projeto (Clean Architecture)

- `LegalScraper.Domain`: Entidades centrais (`Processo`, `Parte`, `Andamento`).
- `LegalScraper.Application`: Casos de uso (Mediator, DTOs, Interfaces).
- `LegalScraper.Infrastructure`: Persistência (EF Core) e Serviços externos (Playwright Scrapers).
- `LegalScraper.API`: Web API em ASP.NET Core expondo os endpoints e o Swagger.

## Como Rodar o Projeto

### Pré-requisitos

1. **.NET SDK** instalado (versão 8+).
2. Opcional: Navegador configurado para acesso à internet (para o Playwright).

### Instalação e Execução

Na pasta raiz do projeto (`/teste_tecnico_webscraping`), primeiro compile a solução para que os assets do Playwright sejam restaurados para `bin/Debug/net*`:

```bash
# Compilar a solução (gera os binários em src/LegalScraper.API/bin/Debug/net*/)
dotnet build
```

- Opção A — Recomendada para Linux/macOS (via Node.js/npx):
  
  Esta é a forma mais robusta em ambientes Linux, pois não depende do PowerShell.

```bash
npx playwright install --with-deps
```

- Opção B — Via .NET CLI (se preferir manter tudo no ecossistema .NET):

Primeiro, restaure as ferramentas e compile o projeto para garantir que os assets do Playwright sejam gerados:

```bash
dotnet build
# O comando abaixo usa o projeto que contém o Playwright como referência
dotnet playwright install -p src/LegalScraper.Infrastructure/LegalScraper.Infrastructure.csproj
```

_Nota: Se o comando `playwright` não for encontrado, instale-o globalmente: `dotnet tool install --global Microsoft.Playwright.CLI` (embora `npx` seja preferível)._

- Opção C — Usar o script gerado pela build (apenas se tiver `pwsh` instalado):

Por que o comando `bash src/LegalScraper.API/bin/Debug/net*/playwright.sh install` que aparece em alguns tutoriais pode falhar?

- O pacote NuGet do `Microsoft.Playwright` normalmente gera um wrapper PowerShell (`playwright.ps1`) e coloca o diretório `.playwright` em `bin/Debug/net*` durante a restauração/build. Nem sempre há um `playwright.sh` no `bin` — por isso o comando `bash .../playwright.sh` pode resultar em "Arquivo ou diretório inexistente". As instruções acima cobrem o uso correto em Windows e em Linux/macOS (via `pwsh`).

Para rodar a API (após instalar navegadores conforme uma das opções acima):

```bash
cd src/LegalScraper.API
dotnet run
```

Após o `dotnet run`, a API estará disponível nas portas padrão (ex.: `http://localhost:5000` ou `https://localhost:5001`). A interface do Swagger fica em `/swagger/index.html`.

A API subirá na porta padrão (ex: `http://localhost:5000` ou `https://localhost:5001`).
Você pode acessar a interface do **Swagger** em `/swagger/index.html`.

## Como Usar a API

### 1. Disparar a extração (Web Scraping)

O scraping funciona via API para fins de teste prático.

**POST** `/processos/scrape`
Body (JSON list):

```json
["1501983-25.2022.8.26.0022", "0010263-82.2026.5.15.0052"]
```

_Atenção aos Captchas:_ A aplicação está configurada para subir o navegador em modo **Headful** (com interface visual). Se o Playwright detectar um CAPTCHA (comum no TJSP e TRT), ele enviará um aviso nos logs do console e **aguardará 30 a 45 segundos** para que você (o usuário assistido) resolva o captcha na janela que abrir. Em um servidor em produção, integraríamos serviços corporativos como _AntiCaptcha_ / _2Captcha_.

### 2. Listar Processos Armazenados

**GET** `/processos`
Retorna um JSON listando todos os processos coletados e persistidos no banco de dados SQLite.

### 3. Consultar Processo Específico

**GET** `/processos/{numeroProcesso}`
Retorna um JSON detalhado contendo a Capa, Partes e Andamentos do processo específico.
