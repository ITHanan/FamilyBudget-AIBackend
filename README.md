# FamilyBudget AI Backend

ASP.NET Core 9 Web API using Clean Architecture:

- `src/Domain`: entities, enums, domain exceptions, no external dependencies
- `src/Application`: DTOs and service interfaces, depends only on Domain
- `src/Infrastructure`: EF Core, SQL Server, OpenAI integration, Hangfire job implementations
- `src/Api`: controllers, JWT auth, Swagger, CORS, DI setup

## Requirements

- .NET 9 SDK
- Docker Desktop

## Run SQL Server

```bash
docker compose up -d
```

The API uses:

```text
Server=localhost,14333;Database=FamilyBudgetAI;User Id=sa;Password=Your_strong_password123;TrustServerCertificate=True;
```

## Apply Database Migrations

```bash
dotnet tool restore
dotnet restore
dotnet ef database update --project src/Infrastructure/Infrastructure.csproj --startup-project src/Api/Api.csproj
```

The initial migration has already been generated in `src/Infrastructure/Migrations`.

## OpenAI API Key

Do not put the key in frontend code. Set it for the backend with user secrets:

```bash
dotnet user-secrets set "OpenAI:ApiKey" "YOUR_OPENAI_API_KEY" --project src/Api/Api.csproj
```

Optional model override:

```bash
dotnet user-secrets set "OpenAI:Model" "gpt-4o-mini" --project src/Api/Api.csproj
```

## Use Ollama Instead Of OpenAI

OpenAI settings remain available. To use free local Ollama for development:

1. Install Ollama from `https://ollama.com/download`.
2. Pull a local model:

```bash
ollama pull llama3.2
```

3. Tell the backend to use Ollama:

```bash
dotnet user-secrets set "AI:Provider" "Ollama" --project src/Api/Api.csproj
dotnet user-secrets set "Ollama:BaseUrl" "http://localhost:11434" --project src/Api/Api.csproj
dotnet user-secrets set "Ollama:Model" "llama3.2" --project src/Api/Api.csproj
```

4. Restart the backend.

To switch back later:

```bash
dotnet user-secrets set "AI:Provider" "OpenAI" --project src/Api/Api.csproj
```

## Run Backend

```bash
dotnet restore
dotnet run --project src/Api/Api.csproj
```

API:

- Base URL: `http://localhost:5001`
- Swagger: `http://localhost:5001/swagger`
- Hangfire dashboard: `http://localhost:5001/hangfire`

This machine already has Docker Desktop listening on port `5000`, so the project is configured to use `5001`.

## Implemented Endpoints

- `POST /api/auth/register`
- `POST /api/auth/login`
- `GET /api/auth/me`
- `GET /api/subscriptions`
- `GET /api/subscriptions/{id}`
- `POST /api/subscriptions`
- `PUT /api/subscriptions/{id}`
- `DELETE /api/subscriptions/{id}`
- `GET /api/dashboard/summary`
- `GET /api/ai/conversations`
- `POST /api/ai/conversations`
- `GET /api/ai/conversations/{id}`
- `POST /api/ai/conversations/{id}/messages`
- `DELETE /api/ai/conversations/{id}`
- `GET /api/notifications`
- `POST /api/notifications/mark-read/{id}`
