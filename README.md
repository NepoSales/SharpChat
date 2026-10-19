# SharpChat

Realtime chat for developers, built with .NET 10, SignalR, PostgreSQL, Redis and React.
Project decisions are in [DECISIONS.md](DECISIONS.md) and progress is tracked in [IMPLEMENTATION_PLAN.md](IMPLEMENTATION_PLAN.md).

## Running

Requirements: Docker with Docker Compose.

```bash
docker compose up --build
```

| Service | Address |
|---|---|
| API | http://localhost:8080 |
| Liveness | http://localhost:8080/health |
| Readiness (Postgres + Redis) | http://localhost:8080/health/ready |

## Development

Requirements: .NET 10 SDK and Docker (integration tests start their own containers with Testcontainers).

```bash
docker compose up -d postgres redis   # dependencies only
dotnet run --project src/SharpChat.Api
dotnet test
```

Database migrations are applied automatically on startup in Development. To add one:

```bash
dotnet tool restore
dotnet ef migrations add <Name> -p src/SharpChat.Infrastructure -s src/SharpChat.Api -o Persistence/Migrations
```
