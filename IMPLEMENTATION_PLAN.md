# SharpChat: Implementation Plan

Working checklist for building SharpChat, phase by phase. `DECISIONS.md` says **what** and **why**; this file tracks **how far along** we are.

Last update: 2026-10-20

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
- Pushes are done only by the user. Claude notifies at strategic points: after a phase is merged into `develop`, at the end of a session with a phase in progress, and before risky changes.
- Full rules: `DECISIONS.md`, section 9 (Git workflow).

### Paths

Backend paths in this file (`src/...`, `tests/...`, `Directory.*.props`) are relative to `backend/`. .NET commands run from `backend/`; `docker compose` runs from the repository root.

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
| **Current phase** | Phase 4: React frontend |
| **Next step** | SignalR connection + message composer, then history with infinite scroll |
| **Blockers** | None |
| **Last session** | 2026-10-20: Phase 4 started (tokens, logo, auth cookie, API client, login/register) |

---

## Overview

- [x] **Phase 0**: Foundation (solution, Docker Compose, health check)
- [x] **Phase 1**: Authentication (register, login, JWT + refresh token)
- [x] **Phase 2**: Conversations and members (REST)
- [x] **Phase 3**: Realtime messaging, persistence, history (**MVP**)
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
- [x] ASP.NET Core Identity with a `User` entity, stored in PostgreSQL
- [x] `POST /auth/register` with validation (email, username, password rules)
- [x] `POST /auth/login` returns access token (short-lived JWT) + refresh token
- [x] Refresh token entity: stored hashed, expiry, revocation, rotation on use
- [x] `POST /auth/refresh` (rotates the refresh token; reuse of a revoked token revokes the whole family)
- [x] `POST /auth/logout` (revokes the refresh token)
- [x] `GET /users/me` (protected endpoint to prove auth works)
- [x] JWT settings in configuration (issuer, audience, key, lifetimes); key never committed
- [x] Current user service reading the user id **from claims only**
- [x] Global error handling middleware with `ProblemDetails`
- [x] Unit tests: token generation, validation rules
- [x] Integration tests: register → login → access protected endpoint → refresh → logout; invalid/expired token returns 401

### Done when
- [x] A user can register, log in and call a protected endpoint; refresh and logout work
- [x] Tests pass

**Summary** (finished 2026-10-18)
- **Built**: register, login, refresh (with rotation and reuse detection) and logout, plus `GET /users/me`. Every endpoint requires a JWT unless it is marked `[AllowAnonymous]`. Errors are `ProblemDetails` with a `code` field.
- **Key files**:
  - Domain: `Domain/Auth/RefreshToken.cs` (token rules).
  - Application: `Application/Auth/AuthService.cs` (use cases), `Application/Common/Result.cs` + `Error.cs` (results without exceptions), `Application/Abstractions/*` (ports).
  - Infrastructure: `Identity/IdentityService.cs` (UserManager, lockout, unique-violation race), `Authentication/JwtTokenService.cs` + `JwtOptions.cs`, `Persistence/Configurations/*`, `DependencyInjection.cs` (Identity options, JWT bearer).
  - Api: `Controllers/AuthController.cs`, `Common/ErrorResults.cs` (Error → ProblemDetails), `Services/CurrentUser.cs` (reads `sub`).
- **How to run / test**: run `dotnet user-secrets set "Jwt:SigningKey" ...` once for `dotnet run`, and `cp .env.example .env` once for Docker. `dotnet test` runs 18 unit and 15 integration tests.
- **Decisions taken during the phase**: all added to DECISIONS.md: auth by default, the refresh token design, token lifetimes, the password and lockout policy, Identity without roles, and where the JWT key lives. One new open decision: where the browser stores the refresh token (Phase 4).
- **Notes for next phases**:
  - JWT claims are `sub` (user id), `name` (username) and `jti`, with `MapInboundClaims = false`. In Phase 3 SignalR needs an `IUserIdProvider` that reads `sub`, plus `OnMessageReceived` for the `access_token` query string.
  - Identity tables are renamed to `Users`, `UserClaims`, `UserLogins` and `UserTokens`. `ApplicationUser` stays in Infrastructure, and Domain refers to users by `Guid` only.
  - Use cases are concrete classes (`AuthService`, `UserService`) registered in `Application/DependencyInjection.cs`. Controllers map `Result` with `this.ToProblem(error)`.
- **Known limitations / leftovers**:
  - Expired or revoked refresh tokens are never deleted (cleanup job, later).
  - Lockout allows locking someone else's account; rate limiting comes in Phase 7.
  - No email confirmation or password reset (not in scope).

---

## Phase 2: Conversations and members

**Goal**: create groups, add/remove people.

### Tasks
- [x] Domain: `Conversation`, `ConversationMember` with roles (`Owner`, `Admin`, `Member`)
- [x] Domain rules: max members (from config), creator becomes `Owner`, only `Owner`/`Admin` manage members, `Owner` can't be removed
- [x] EF Core mapping, constraints and migration
- [x] `POST /conversations` (create, creator is `Owner`)
- [x] `GET /conversations` (only conversations the user belongs to)
- [x] `GET /conversations/{id}` (members only)
- [x] `POST /conversations/{id}/members` (add member)
- [x] `DELETE /conversations/{id}/members/{userId}` (remove member, or leave)
- [x] `PATCH /conversations/{id}/members/{userId}` (change role)
- [x] Membership check as a reusable application service (will be reused by the Hub)
- [x] Non-members get 404 (don't reveal that the conversation exists)
- [x] Unit tests: domain rules (limit, roles, owner protection)
- [x] Integration tests: membership enforcement, role permissions

### Done when
- [x] Groups can be created and managed through the API, with permissions enforced on the server
- [x] Tests pass

**Summary** (finished 2026-10-18)
- **Built**: you can create a conversation (with optional initial members), list your conversations, see details, add members by username, change roles and remove members or leave. Every rule lives in the `Conversation` aggregate. Concurrent changes are guarded by `xmin`, so the member limit holds under a race.
- **Key files**:
  - Domain: `Domain/Conversations/Conversation.cs` (all rules), `ConversationMember.cs`, `ConversationRole.cs`, `ConversationErrors.cs`, and `Domain/Common/Result.cs` + `Error.cs` (moved from Application, now with a non-generic `Result`).
  - Application: `Conversations/ConversationService.cs` (commands), `Conversations/MembershipService.cs` (reusable membership check), `Conversations/ChatOptions.cs` (`Chat:MaxGroupMembers`), `Abstractions/IConversationRepository.cs`, `IConversationQueries.cs`, `CurrentUserExtensions.cs` (`RequiredUserId()`).
  - Infrastructure: `Persistence/Configurations/ConversationConfiguration.cs`, `Persistence/Repositories/ConversationRepository.cs`, `Persistence/Queries/ConversationQueries.cs`.
  - Api: `Controllers/ConversationsController.cs`, and `JsonStringEnumConverter` in `Program.cs`.
  - Tests: `IntegrationTests/Infrastructure/TestUser.cs` (logged-in user with its own HttpClient) and `Conversations/ConversationApi.cs` (helpers).
- **How to run / test**: `dotnet test` runs 35 unit and 23 integration tests. To use a smaller member limit in a test, use `factory.WithWebHostBuilder(b => b.UseSetting("Chat:MaxGroupMembers", "3"))`.
- **Decisions taken during the phase**: all added to DECISIONS.md: the membership rules (section 7), `Result` in Domain, the repository/queries split, concurrency through `UpdatedAt` + `xmin`, and enums as strings.
- **Notes for next phases**:
  - Phase 3: the hub should use `MembershipService.IsMemberAsync` for each send, and `IConversationQueries.ListForUserAsync` to join groups on connect. When a member is added or removed, the server has to update their SignalR groups. Hook into `ConversationService.AddMemberAsync` and `RemoveMemberAsync`, for example through a notifier interface.
  - Phase 5: add `LastReadMessageId` to `ConversationMember`, as planned in section 7.
  - The conversation list is ordered by `UpdatedAt`. Phase 3 should change it to the last message time.
- **Known limitations / leftovers**:
  - No ownership transfer, rename or delete for conversations.
  - No user search endpoint; the frontend will need one in Phase 4 to add members.
  - The conversation list isn't paginated.

---

## Phase 3: Realtime messaging (MVP)

**Goal**: working chat.

### Tasks
- [x] Domain: `Message` (client-generated Id, conversation, sender, raw markdown content, `SentAt`, `ParentMessageId`, `EditedAt`, `DeletedAt`)
- [x] EF Core mapping, migration, index on (`ConversationId`, `SentAt`, `Id`)
- [x] Message size limit (configurable) and content validation
- [x] SignalR `ChatHub` with JWT auth (`access_token` query string configured explicitly)
- [x] ~~On connect: add the connection to SignalR groups~~ → replaced: delivery goes to the members' user ids, read from the database on every send (see DECISIONS.md)
- [x] ~~Group sync when membership changes~~ → not needed with the approach above (a test covers removed and added members)
- [x] `SendMessage` hub method: validate → check membership → **persist** → broadcast → ack the sender
- [x] `GET /conversations/{id}/messages?before={cursor}&limit=50` (cursor-based pagination by `SentAt`, `Id`)
- [x] Integration tests with a real SignalR client: two users exchange messages; non-member can't send or receive
- [x] Integration tests: pagination with equal timestamps, no duplicates or gaps
- [x] Security: patched CVE-2026-70159 (SignalR DoS) by moving every Microsoft package to 10.0.13

### Done when
- [x] Two clients (e.g. a test or a small script) can chat in real time, and history loads with cursor pagination
- [x] Tests pass

**Summary** (finished 2026-10-19)
- **Built**: the realtime chat MVP. A SignalR hub at `/hubs/chat` handles `SendMessage`: it checks membership, persists the message, broadcasts `MessageReceived` to every connection of every member, then acks the sender. History is served by `GET /conversations/{id}/messages` with an opaque cursor (`before`). Sending the same message Id twice stores the message once. The conversation list is now ordered by the last message.
- **Key files**:
  - Domain: `Domain/Messages/Message.cs`, `MessageErrors.cs`.
  - Application: `Messages/MessageService.cs` (send and history), `Messages/MessageCursor.cs` (encodes `SentAt` ticks + `Id` in base64url), `Messages/MessageContracts.cs`, `Abstractions/IChatNotifier.cs`, `IMessageRepository.cs`, `IMessageQueries.cs`, and `Conversations/ChatOptions.cs` (`MaxMessageLength`, `DefaultHistoryPageSize`).
  - Infrastructure: `Persistence/Configurations/MessageConfiguration.cs`, `Persistence/Queries/MessageQueries.cs` (row value comparison `(SentAt, Id) < (...)`), and `ConversationQueries.ListMembersAsync` / `LastMessageAt`.
  - Api: `Hubs/ChatHub.cs` (with `IChatClient`), `Hubs/HubResponse.cs` (hub results use the same error codes as REST), `Hubs/SignalRChatNotifier.cs`, `Hubs/HubAuthentication.cs` (`access_token` accepted only on `/hubs`, `sub` used as the SignalR user id), `Controllers/MessagesController.cs`.
  - Tests: `IntegrationTests/Messaging/ChatClient.cs`, a real WebSocket client that sends the token in the query string like a browser.
- **How to run / test**: `dotnet test` runs 35 unit and 38 integration tests. To control time in a test, use `factory.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton<TimeProvider>(fakeClock)))`. Start the fake clock at `DateTimeOffset.UtcNow`, because a clock in the past makes the JWT look expired.
- **Decisions taken during the phase**: all added to DECISIONS.md: delivery to user ids instead of groups, hub responses as `{ data, error }`, `SentAt` truncated to microseconds, the opaque cursor, stateful reconnect kept off, and patch versions bumped for the CVE.
- **Notes for next phases**:
  - Phase 4: the frontend connects with `accessTokenFactory` (the SignalR JS client already sends it as `access_token`). It receives `MessageReceived` and dedupes by `id`, because the sender also gets the event. Loading older pages uses `nextCursor` as `before`.
  - Phase 5: the basic idempotency check exists (an existing Id with the same sender returns the same ack and broadcasts again). Still missing: two *simultaneous* sends with the same Id, which hit the primary key and currently surface as a hub exception, plus delivered/read status.
  - Phase 7: `Clients.Users(...)` with the Redis backplane costs one publish per member. That is acceptable because of the 100-member cap.
- **Known limitations / leftovers**:
  - No "conversation added/removed" event, so a user added to a conversation only sees it after reloading the list. Planned for Phase 4.
  - Hub methods don't have their own rate limiting yet (Phase 7).

---

## Phase 4: React frontend

**Goal**: usable dev chat in the browser.

### Tasks
- [x] `frontend/` with Vite + React + TypeScript, ESLint + Prettier (scaffold already created by the user)
- [x] Design tokens as CSS variables (palette from `DECISIONS.md` section 6), JetBrains Mono for code
- [x] Logo `#Chat` and compact `#` favicon
- [x] API client with access token handling and automatic refresh
- [x] Refresh token moved to an `HttpOnly` cookie (backend change, open decision closed)
- [x] Pages: register, login, protected routes
- [ ] Layout: sidebar with conversations, main chat area, message input (done except the input, which comes with SignalR)
- [x] Create conversation and manage members UI
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
- [ ] `GET /messages/{id}/replies` with cursor pagination
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
| 2026-10-20 | 4 | Swagger UI at `/swagger` (chore, merged into develop and the phase branch). App shell: sidebar, conversation page, new conversation dialog, members panel (add, role, remove, leave), TanStack Query. Proxy chain checked end to end | SignalR + composer |
| 2026-10-20 | 4 | Branch `phase-4-frontend`. Scaffold committed, design tokens, logo/favicon, Prettier. Refresh token moved to an HttpOnly cookie (backend + tests). API client (in-memory token, refresh serialized across tabs), auth context, login/register, route guards, Vite `/api` proxy | Run integration tests; Layout |
| 2026-10-19 | — | Frontend folder decided: `frontend/` (not `web/`). Vite scaffold exists there, uncommitted. Local .NET runtime still 10.0.12 (user to update) | Phase 4: review scaffold |
| 2026-10-19 | 3 | Realtime MVP: hub, send/receive, history with cursor, idempotent resend; Microsoft packages → 10.0.13 (CVE-2026-70159); code comments removed project-wide | Start Phase 4 |
| 2026-10-18 | — | `/api` prefix removed; push rule: only the user pushes, Claude notifies | — |
| 2026-10-18 | 2 | Conversations and members: aggregate rules, REST endpoints, queries, concurrency; Phase 2 done | Start Phase 3 |
| 2026-10-18 | — | Backend moved to `backend/` (solution, props, src, tests); Compose context updated | Start Phase 2 |
| 2026-10-18 | 1 | Auth: register/login/refresh/logout, `/users/me`, auth by default, ProblemDetails; Phase 1 done | Start Phase 2 |
| 2026-10-18 | 0 | Solution, Docker Compose, health checks, integration test infra; Phase 0 done | Start Phase 1 |
| 2026-10-18 | — | Decisions documented in `DECISIONS.md`, implementation plan created | Start Phase 0 |
