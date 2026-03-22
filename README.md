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

#### 1. Compilar o projeto

```bash
dotnet build
```

O `dotnet build` restaura os pacotes e copia os assets do Playwright para `bin/Debug/net*/`.

#### 2. Instalar os navegadores

Após compilar, instale o navegador Chromium e suas dependências de sistema:

```bash
npx playwright install --with-deps chromium
```

> **Por que `npx`?** Em ambientes Linux, o wrapper gerado pelo NuGet (`playwright.ps1`) requer PowerShell. Usar `npx` é a abordagem mais robusta e independente de plataforma.

#### 3. Rodar a API

```bash
cd src/LegalScraper.API
dotnet run
```

Após o `dotnet run`, a API estará disponível nas portas padrão (ex.: `http://localhost:5000` ou `https://localhost:5001`). A interface do Swagger fica em `/swagger/index.html`.

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
Retorna um JSON detalhado contendo a Capa, Partes e Andamentos do processo específico. O retorno também inclui os campos:
- `pdfDisponivel` (`bool`): indica se o PDF foi baixado e está armazenado.
- `pdfNome` (`string`): nome sugerido do arquivo PDF.

### 4. Baixar PDF do Processo

Essa ação foi necessária para os casos em que o processo está salvo em pdf, como no site https://pje.trt2.jus.br/consultaprocessual/
Nota: Implementar funcionalidade de processamento do PDF via agente de IA para extração de dados.

**GET** `/processos/{numeroProcesso}/pdf`

Retorna o PDF armazenado do processo como download direto (`Content-Type: application/pdf`). O número do processo deve ser URL-encoded se contiver caracteres especiais.

Exemplo:
```
GET /processos/0001234-56.2023.5.02.0001/pdf
```

Resposta de sucesso: arquivo PDF com o nome original do download.
Resposta de erro (`404`): `{ "message": "PDF não disponível para o processo ..." }` — indica que o processo existe mas o PDF ainda não foi coletado ou o botão de download não estava disponível durante o scraping.
