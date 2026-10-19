# SharpChat

Realtime chat for developers, built with .NET 10, SignalR, PostgreSQL, Redis and React.
Project decisions are in [DECISIONS.md](DECISIONS.md) and progress is tracked in [IMPLEMENTATION_PLAN.md](IMPLEMENTATION_PLAN.md).

## Running

Requirements: Docker with Docker Compose.

```bash
cp .env.example .env          # once: then set JWT_SIGNING_KEY (openssl rand -base64 48)
docker compose up --build
```

| Service | Address |
|---|---|
| API | http://localhost:8080 |
| Liveness | http://localhost:8080/health |
| Readiness (Postgres + Redis) | http://localhost:8080/health/ready |

## Development

Requirements: .NET 10 SDK and Docker (integration tests start their own containers with Testcontainers).
The backend lives in `backend/`; run the commands below from there.

```bash
cd backend
dotnet user-secrets set "Jwt:SigningKey" "$(openssl rand -base64 48)" --project src/SharpChat.Api   # once
docker compose -f ../docker-compose.yml up -d postgres redis   # dependencies only
dotnet run --project src/SharpChat.Api
dotnet test
```

The JWT signing key is never committed: it comes from user-secrets locally and from `.env` in Docker. The API refuses to start without it.

Database migrations are applied automatically on startup in Development. To add one:

```bash
dotnet tool restore
dotnet ef migrations add <Name> -p src/SharpChat.Infrastructure -s src/SharpChat.Api -o Persistence/Migrations
```

## API

| Method | Path | Auth | Description |
|---|---|---|---|
| POST | `/api/auth/register` | — | Create an account |
| POST | `/api/auth/login` | — | Get an access token (15 min) and a refresh token (14 days) |
| POST | `/api/auth/refresh` | — | Exchange a refresh token for a new pair (rotation) |
| POST | `/api/auth/logout` | — | Revoke the session of a refresh token |
| GET | `/api/users/me` | Bearer | Current user |
| POST | `/api/conversations` | Bearer | Create a conversation (optionally with `memberUserNames`) |
| GET | `/api/conversations` | Bearer | Conversations I'm a member of |
| GET | `/api/conversations/{id}` | Bearer | Conversation details and members |
| POST | `/api/conversations/{id}/members` | Bearer (owner/admin) | Add a member by username |
| PATCH | `/api/conversations/{id}/members/{userId}` | Bearer (owner) | Change a member's role |
| DELETE | `/api/conversations/{id}/members/{userId}` | Bearer | Remove a member, or leave (own id) |

Non-members get `404` for a conversation, so they can't tell whether it exists.

Errors use [ProblemDetails](https://www.rfc-editor.org/rfc/rfc9457) with a machine-readable `code` (e.g. `email_taken`, `invalid_credentials`).
