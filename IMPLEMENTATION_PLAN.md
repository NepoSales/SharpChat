# SharpChat: Implementation Plan

Working checklist for building SharpChat, phase by phase. `DECISIONS.md` says **what** and **why**; this file tracks **how far along** we are.

Last update: 2026-10-18

---

## How to use this file

- **Start of a session**: read [Current status](#current-status) and the summary of the last finished phase. That is enough context to continue.
- **During a phase**: tick tasks (`- [x]`) as they are done. Add tasks if the phase needs more work than planned.
- **End of a phase**:
  1. Check that every task and the "Done when" criteria are ticked.
  2. Tick the phase in the [Overview](#overview).
  3. Fill in the phase **Summary** (template below).
  4. Update [Current status](#current-status).
  5. If a decision changed, update `DECISIONS.md` (section 11, decision log) too.
- A phase is only finished when its result can be shown on its own (see `DECISIONS.md`, section 9) and its tests pass.

### Git workflow

- `main`: only stable releases. The first merge into `main` is the first stable version of the app.
- `develop`: integration branch. Every finished phase is merged here with `--no-ff`.
- `phase-N-<name>` (e.g. `phase-1-auth`): one branch per phase, created from `develop` and deleted after the merge.
- Pushing to `origin` is done when it makes sense (e.g. after a phase is merged), always confirmed first.

### Summary template

```markdown
**Summary** (finished YYYY-MM-DD)
- **Built**: what now exists, in one or two lines.
- **Key files**: the files/folders someone should open first.
- **How to run / test**: commands.
- **Decisions taken during the phase**: anything not in DECISIONS.md yet (and whether it was added there).
- **Known limitations / leftovers**: what was postponed and to which phase.
```

---

## Current status

| | |
|---|---|
| **Current phase** | Phase 1: Authentication |
| **Next step** | Add ASP.NET Core Identity with the `User` entity and its migration |
| **Blockers** | None |
| **Last session** | 2026-10-18: Phase 0 finished (solution, Docker Compose, health checks, Testcontainers) |

---

## Overview

- [x] **Phase 0**: Foundation (solution, Docker Compose, health check)
- [ ] **Phase 1**: Authentication (register, login, JWT + refresh token)
- [ ] **Phase 2**: Conversations and members (REST)
- [ ] **Phase 3**: Realtime messaging, persistence, history (**MVP**)
- [ ] **Phase 4**: React frontend (dark theme, markdown, code blocks)
- [ ] **Phase 5**: Reliability (idempotency, reconnect sync, delivered/read)
- [ ] **Phase 6**: Threads, editing and deletion
- [ ] **Phase 7**: Scale (Redis backplane, presence, typing, rate limiting)
- [ ] **Phase 8**: Observability, test review, README (**portfolio ready**)
- [ ] **Later**: Deploy (live demo)

---

## Phase 0: Foundation

**Goal**: the project runs with one command.

### Tasks
- [x] Create `SharpChat.sln` and projects: `SharpChat.Domain`, `SharpChat.Application`, `SharpChat.Infrastructure`, `SharpChat.Api` (.NET 10)
- [x] Create test projects: `SharpChat.UnitTests`, `SharpChat.IntegrationTests` (xUnit)
- [x] Set project references following the dependency rule (`Api → Application → Domain`, `Infrastructure → Application`)
- [x] Shared build settings (`Directory.Build.props`: nullable, warnings as errors, central package management)
- [x] `.gitignore`, `.editorconfig`
- [x] `Dockerfile` for the API (multi-stage build)
- [x] `docker-compose.yml` with API + PostgreSQL + Redis (named volumes, env vars)
- [x] Configuration via `appsettings.json` + environment variables (connection strings, `MaxGroupMembers: 100`)
- [x] EF Core `DbContext` wired to PostgreSQL, first (empty) migration, migrations applied on startup in Development
- [x] Health checks: `/health` (liveness) and `/health/ready` (Postgres + Redis)
- [x] Integration test infrastructure: `WebApplicationFactory` + Testcontainers (Postgres, Redis)
- [x] First integration test: health check returns healthy
- [x] Basic README section: how to run (`docker compose up`)

### Done when
- [x] `docker compose up` starts API, Postgres and Redis, and `/health/ready` returns healthy
- [x] `dotnet test` passes

**Summary** (finished 2026-10-18)
- **Built**: .NET 10 solution with the four Clean Architecture projects and two test projects. Docker Compose runs API + Postgres 17 + Redis 7. Health checks are at `/health` (liveness) and `/health/ready` (Postgres + Redis). Integration tests run against real containers.
- **Key files**: `Directory.Build.props` (shared settings, warnings as errors, `latest-recommended` analyzers), `Directory.Packages.props` (all package versions), `src/SharpChat.Infrastructure/DependencyInjection.cs` (`AddInfrastructure`: DbContext, Redis, health checks), `src/SharpChat.Api/Program.cs`, `src/SharpChat.Api/Dockerfile`, `docker-compose.yml`, `tests/SharpChat.IntegrationTests/Infrastructure/SharpChatApiFactory.cs` (shared `[Collection(ApiTestGroup.Name)]` fixture).
- **How to run / test**: `docker compose up --build`; `dotnet test` (needs Docker); migrations: `dotnet tool restore` + `dotnet ef migrations add <Name> -p src/SharpChat.Infrastructure -s src/SharpChat.Api -o Persistence/Migrations`.
- **Decisions taken during the phase**:
  - HTTPS redirection/HSTS only outside Development (added to DECISIONS.md).
  - Microsoft packages pinned to 10.0.12 to match the installed runtime. StackExchange.Redis stays on 2.x because the SignalR Redis backplane (Phase 7) depends on 2.x.
  - xUnit v2 (template default). Test names use underscores (CA1707 disabled in `tests/.editorconfig`).
  - Redis client uses `AbortOnConnectFail=false`, so the API starts without Redis and readiness reports it.
  - `GSS Encryption Mode=Disable` in the Compose connection string (the slim image lacks Kerberos libs).
  - Config keys: `ConnectionStrings:Postgres`, `ConnectionStrings:Redis`, `Chat:MaxGroupMembers` (bound to an options class in Phase 2).
- **Known limitations / leftovers**: `InitialCreate` is an empty migration (first tables come in Phase 1). EF logs a warning about no `IEntityTypeConfiguration` until then. `UnitTests` has no tests yet. Dev credentials (`sharpchat`/`sharpchat`) are defaults meant for local use only; they can be overridden through `POSTGRES_*` env vars.

---

## Phase 1: Authentication

**Goal**: authenticated API.

### Tasks
- [ ] ASP.NET Core Identity with a `User` entity, stored in PostgreSQL
- [ ] `POST /api/auth/register` with validation (email, username, password rules)
- [ ] `POST /api/auth/login` returns access token (short-lived JWT) + refresh token
- [ ] Refresh token entity: stored hashed, expiry, revocation, rotation on use
- [ ] `POST /api/auth/refresh` (rotates the refresh token; reuse of a revoked token revokes the whole family)
- [ ] `POST /api/auth/logout` (revokes the refresh token)
- [ ] `GET /api/users/me` (protected endpoint to prove auth works)
- [ ] JWT settings in configuration (issuer, audience, key, lifetimes); key never committed
- [ ] Current user service reading the user id **from claims only**
- [ ] Global error handling middleware with `ProblemDetails`
- [ ] Unit tests: token generation, validation rules
- [ ] Integration tests: register → login → access protected endpoint → refresh → logout; invalid/expired token returns 401

### Done when
- [ ] A user can register, log in and call a protected endpoint; refresh and logout work
- [ ] Tests pass

**Summary**: _not finished yet_

---

## Phase 2: Conversations and members

**Goal**: create groups, add/remove people.

### Tasks
- [ ] Domain: `Conversation`, `ConversationMember` with roles (`Owner`, `Admin`, `Member`)
- [ ] Domain rules: max members (from config), creator becomes `Owner`, only `Owner`/`Admin` manage members, `Owner` can't be removed
- [ ] EF Core mapping, constraints and migration
- [ ] `POST /api/conversations` (create, creator is `Owner`)
- [ ] `GET /api/conversations` (only conversations the user belongs to)
- [ ] `GET /api/conversations/{id}` (members only)
- [ ] `POST /api/conversations/{id}/members` (add member)
- [ ] `DELETE /api/conversations/{id}/members/{userId}` (remove member, or leave)
- [ ] `PATCH /api/conversations/{id}/members/{userId}` (change role)
- [ ] Membership check as a reusable application service (will be reused by the Hub)
- [ ] Non-members get 404 (don't reveal that the conversation exists)
- [ ] Unit tests: domain rules (limit, roles, owner protection)
- [ ] Integration tests: membership enforcement, role permissions

### Done when
- [ ] Groups can be created and managed through the API, with permissions enforced on the server
- [ ] Tests pass

**Summary**: _not finished yet_

---

## Phase 3: Realtime messaging (MVP)

**Goal**: working chat.

### Tasks
- [ ] Domain: `Message` (client-generated Id, conversation, sender, raw markdown content, `SentAt`, `ParentMessageId`, `EditedAt`, `DeletedAt`)
- [ ] EF Core mapping, migration, index on (`ConversationId`, `SentAt`, `Id`)
- [ ] Message size limit (configurable) and content validation
- [ ] SignalR `ChatHub` with JWT auth (`access_token` query string configured explicitly)
- [ ] On connect: add the connection to SignalR groups for each of the user's conversations
- [ ] Group sync when membership changes (added → join group, removed → leave group)
- [ ] `SendMessage` hub method: validate → check membership → **persist** → broadcast → ack the sender
- [ ] `GET /api/conversations/{id}/messages?before={cursor}&limit=50` (cursor-based pagination by `SentAt`, `Id`)
- [ ] Integration tests with a real SignalR client: two users exchange messages; non-member can't send or receive
- [ ] Integration tests: pagination with equal timestamps, no duplicates or gaps

### Done when
- [ ] Two clients (e.g. a test or a small script) can chat in real time, and history loads with cursor pagination
- [ ] Tests pass

**Summary**: _not finished yet_

---

## Phase 4: React frontend

**Goal**: usable dev chat in the browser.

### Tasks
- [ ] `web/` with Vite + React + TypeScript, ESLint + Prettier
- [ ] Design tokens as CSS variables (palette from `DECISIONS.md` section 6), JetBrains Mono for code
- [ ] Logo `#Chat` and compact `#` favicon
- [ ] API client with access token handling and automatic refresh
- [ ] Pages: register, login, protected routes
- [ ] Layout: sidebar with conversations, main chat area, message input
- [ ] Create conversation and manage members UI
- [ ] SignalR connection (`@microsoft/signalr`) with token, receive and send messages
- [ ] History with infinite scroll upwards (cursor pagination)
- [ ] Markdown rendering with `react-markdown` + `rehype-sanitize`
- [ ] Code blocks highlighted with Shiki (dark theme matching the background)
- [ ] XSS check: messages with raw HTML/scripts are rendered harmless
- [ ] Frontend added to Docker Compose
- [ ] Component tests for the critical parts (message rendering, sanitization)

### Done when
- [ ] Full flow in the browser: register, log in, create a group, chat with markdown and highlighted code
- [ ] `docker compose up` brings up the frontend too

**Summary**: _not finished yet_

---

## Phase 5: Reliability

**Goal**: robust chat (no lost or duplicated messages).

### Tasks
- [ ] Idempotent send: same message Id twice → stored once, sender gets the same ack
- [ ] Client retry with the same Id until acked; pending messages shown as "sending"
- [ ] Reconnect sync: client asks for everything after its last received message, including edits/deletions while offline
- [ ] Delivered status (client acknowledges receipt)
- [ ] Read status (updates `ConversationMember` last read message)
- [ ] Unread counters per conversation
- [ ] Frontend: status indicators (sent → delivered → read), reconnect handling, connection state banner
- [ ] Integration tests: duplicate send, disconnect/reconnect sync, status transitions

### Done when
- [ ] Killing the connection mid-conversation and reconnecting loses and duplicates nothing
- [ ] Tests pass

**Summary**: _not finished yet_

---

## Phase 6: Threads, editing and deletion

**Goal**: full dev chat concept.

### Tasks
- [ ] Thread replies: `ParentMessageId` set, one level only (reply to a reply is rejected)
- [ ] `GET /api/messages/{id}/replies` with cursor pagination
- [ ] Reply count / last reply info on the parent message
- [ ] Edit: author only, sets `EditedAt`, broadcast edit event
- [ ] Delete: author, or `Owner`/`Admin`; soft delete (clear content, set `DeletedAt`), broadcast delete event
- [ ] Edits/deletes included in reconnect sync (Phase 5)
- [ ] Frontend: thread side panel, "edited" label, "message deleted" placeholder, edit/delete actions shown only when allowed
- [ ] Unit tests: thread depth rule, edit/delete permissions
- [ ] Integration tests: permission enforcement on the server, events reach other members

### Done when
- [ ] Threads, editing and deletion work end to end with permissions enforced on the server
- [ ] Tests pass

**Summary**: _not finished yet_

---

## Phase 7: Scale

**Goal**: scales to multiple instances.

### Tasks
- [ ] SignalR Redis backplane
- [ ] Presence (online/offline) stored in Redis, multiple tabs/devices per user handled
- [ ] Typing indicator (throttled on the client, not persisted)
- [ ] Rate limiting (ASP.NET Core middleware) for REST, plus a limit for hub messages
- [ ] Docker Compose with 2 API instances behind a load balancer (e.g. nginx or YARP) to prove it works
- [ ] Frontend: online indicators, "X is typing..."
- [ ] Integration tests: message sent on instance A reaches a client on instance B; rate limit returns 429

### Done when
- [ ] Two API instances run together and users connected to different instances chat normally
- [ ] Tests pass

**Summary**: _not finished yet_

---

## Phase 8: Portfolio ready

**Goal**: observable, tested, well documented.

### Tasks
- [ ] OpenTelemetry instrumentation (ASP.NET Core, EF Core, SignalR, HTTP client)
- [ ] Custom metrics: messages sent, active connections, send latency
- [ ] Serilog → Loki, traces → Jaeger, metrics → Prometheus
- [ ] Grafana with provisioned dashboards (API, messaging, infra)
- [ ] Observability stack in Docker Compose (optional profile)
- [ ] Test coverage review, fill gaps in critical paths
- [ ] CI pipeline (e.g. GitHub Actions: build + tests)
- [ ] README: overview, screenshots/GIF, architecture diagram, how to run, design decisions (link to `DECISIONS.md`)

### Done when
- [ ] A visitor can understand, run and evaluate the project from the README alone
- [ ] Dashboards show live data while chatting

**Summary**: _not finished yet_

---

## Later: Deploy

- [ ] Choose hosting (to be decided in `DECISIONS.md`, "Open decisions")
- [ ] HTTPS/WSS, secrets management, production configuration
- [ ] Live demo link in the README

---

## Session log

Short notes per session (newest first). Keep each entry to a few lines.

| Date | Phase | What was done | Next step |
|---|---|---|---|
| 2026-10-18 | 0 | Solution, Docker Compose, health checks, integration test infra; Phase 0 done | Start Phase 1 |
| 2026-10-18 | — | Decisions documented in `DECISIONS.md`, implementation plan created | Start Phase 0 |
