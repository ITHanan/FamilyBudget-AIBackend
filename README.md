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

Create a local `.env` file from `.env.example` and set a strong local SQL password:

```bash
copy .env.example .env
```

```bash
docker compose up -d
```

The API uses:

```text
Server=localhost,14333;Database=FamilyBudgetAI;User Id=sa;Password=<your-local-sql-password>;Encrypt=False;TrustServerCertificate=True;
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
- Health: `http://localhost:5001/health`
- Hangfire dashboard: `http://localhost:5001/hangfire`

This machine already has Docker Desktop listening on port `5000`, so the project is configured to use `5001`.

## Deploy To Azure App Service

Publish from the API project:

```bash
dotnet restore src/Api/Api.csproj
dotnet publish src/Api/Api.csproj -c Release -o artifacts/publish
```

If restore/build exits with `Build FAILED` but shows `0 Error(s)`, run MSBuild serially:

```bash
dotnet msbuild src/Api/Api.csproj /t:Restore /nr:false /m:1
dotnet msbuild FamilyBudget-AIBackend.sln /t:Build /p:Configuration=Release /nr:false /m:1
dotnet publish src/Api/Api.csproj --no-restore -c Release -o artifacts/publish /p:BuildInParallel=false /m:1
```

Set these Azure App Service application settings:

```text
ASPNETCORE_ENVIRONMENT=Production
ConnectionStrings__DefaultConnection=Server=tcp:<server>.database.windows.net,1433;Initial Catalog=<database>;User ID=<user>;Password=<password>;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;
Jwt__Key=<at least 32 random characters>
Jwt__Issuer=FamilyBudgetAI
Jwt__Audience=FamilyBudgetAI.Client
Jwt__AccessTokenMinutes=60
Frontend__Url=https://<your-frontend-host>
ApiDocs__EnableSwagger=false
DemoData__Seed=false
AI__Provider=OpenAI
OpenAI__ApiKey=<OpenAI API key>
OpenAI__Model=gpt-4o-mini
```

Run EF migrations against Azure SQL before using the app:

```bash
dotnet ef database update --project src/Infrastructure/Infrastructure.csproj --startup-project src/Api/Api.csproj
```

After deployment, check:

- `https://<your-api-app>.azurewebsites.net/health`
- `https://<your-api-app>.azurewebsites.net/`

Swagger is enabled automatically in development. To expose it in production for a temporary smoke test, set:

```text
ApiDocs__EnableSwagger=true
```

Set it back to `false` after the deployment check unless the API documentation is intentionally public.

## Demo Data

Demo data seeding is opt-in. It creates a `demo` user with a few sample subscriptions and one notification.

```text
DemoData__Seed=true
```

Demo credentials:

```text
username: demo
password: DemoPassword123!
```

Run migrations before enabling seeding in an environment that uses SQL Server.

## Health Check

`GET /health` returns `200 OK` when the ASP.NET Core app is running. Use it for App Service health checks, uptime checks, and post-deployment smoke tests.

## Error And Validation Responses

The API returns `application/problem+json` for validation failures and unhandled errors.

- Invalid request models return `400` with field-level validation errors.
- Service validation failures return `400`.
- Authentication failures return `401`.
- Unexpected production errors are logged server-side and return a generic `500` response with a `traceId`.

## Tests

```bash
dotnet test
```

Coverage includes focused service tests plus integration tests for auth and subscription flows.

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

See [API_DOCUMENTATION.md](API_DOCUMENTATION.md) for request/response examples and operational notes.
