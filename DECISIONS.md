# SharpChat: Project Decisions

Living document with the main decisions for the project. Update it whenever a decision changes, and record why.

Last update: 2026-10-18

---

## 1. Goal

**SharpChat** is a realtime chat application for developers, built for my software engineering portfolio.
Users can create an account, log in, and chat with other people in **group conversations**.

Priorities, in order:
1. **Reliability**: no lost or duplicated messages.
2. **Security**: only members of a conversation can read or send messages in it.
3. **Low latency**: messages delivered as fast as the network allows.

## 2. Concept: Dev chat

A chat made for developer teams and study groups. What makes it different from a generic chat:

| Feature | Description |
|---|---|
| **Markdown messages** | Bold, italic, lists, links, inline code |
| **Code blocks with syntax highlighting** | Fenced code blocks (```` ```csharp ````) rendered with highlighting per language |
| **Message threads** | Reply to a message in a side thread without flooding the main conversation |

Rule: the concept adds these features and nothing else for now. New ideas go to "Open decisions" first.

## 3. Scope

| Item | Decision |
|---|---|
| Concept | **Dev chat** (see section 2) |
| Conversation type | **Group chats** (a 1:1 chat is a group with 2 members) |
| Frontend | **React + TypeScript** |
| Visual theme | **Dark, neutral palette** (see section 6) |
| Group size | **Up to 100 members** per conversation, configurable in `appsettings.json` |
| Attachments | **Out of scope for now** (files and images). Messages are text/markdown only |
| Deploy | **Not now**. Keep everything in Docker so it can be deployed later without changes |
| Timeline | **No fixed deadline**. Work is split into phases, each one showable on its own |

## 4. Architecture

- **Modular monolith**, not microservices. Simpler to build and defend; scales horizontally with Redis.
- **Clean Architecture** layers.
- Dependency rule: `Api → Application → Domain`. `Infrastructure` implements interfaces defined in `Application`. `Domain` depends on nothing.

```
sharpchat/
├── docker-compose.yml             # Orchestrates backend, frontend and dependencies
├── .env.example                   # Template for local secrets (.env is gitignored)
├── DECISIONS.md
├── backend/
│   ├── SharpChat.sln
│   ├── Directory.Build.props      # Shared build settings
│   ├── Directory.Packages.props   # Central package versions
│   ├── src/
│   │   ├── SharpChat.Domain/          # Entities and business rules
│   │   ├── SharpChat.Application/     # Use cases, interfaces, DTOs, validation
│   │   ├── SharpChat.Infrastructure/  # EF Core, repositories, Redis, JWT, Identity
│   │   └── SharpChat.Api/             # Controllers, SignalR Hubs, middleware, Program.cs
│   └── tests/
│       ├── SharpChat.UnitTests/
│       └── SharpChat.IntegrationTests/
└── frontend/                      # React + TypeScript frontend (Vite)
```

## 5. Tech stack

| Layer | Choice |
|---|---|
| Runtime | .NET 10 (LTS) |
| HTTP API | ASP.NET Core Web API (REST) |
| Realtime | SignalR over WebSockets |
| Auth | ASP.NET Core Identity + JWT (access token + refresh token) |
| Database | PostgreSQL + EF Core |
| Cache / scale | Redis (SignalR backplane, presence, rate limiting) |
| Frontend | React + TypeScript + `@microsoft/signalr` |
| Markdown / code | `react-markdown` + `rehype-sanitize` (safe rendering) + Shiki (syntax highlighting) |
| Containers | Docker + Docker Compose |
| Tests | xUnit + Testcontainers |
| Observability | OpenTelemetry (instrumentation) + OpenTelemetry Collector (routing) + Prometheus (metrics) + Loki (logs, via Serilog) + Jaeger (traces) + Grafana (dashboards) + health checks |

**Responsibility split:**
- REST: register, login, refresh token, create/list conversations, manage members, load message history and thread replies.
- SignalR: send and receive messages and thread replies in real time, edit/delete events, typing indicator, presence, delivery/read status.

**Markdown handling:**
- The backend stores the **raw markdown text** as sent. It never stores or generates HTML.
- The frontend renders markdown and highlights code on the client, always through the sanitizer.

**History pagination: cursor-based:**
- The client asks for "N messages before message X" instead of "page 3". New messages arriving don't shift the pages, so nothing is skipped or duplicated.
- Cursor = (`SentAt`, `Id`) of the oldest message the client has. `Id` breaks ties when two messages have the same timestamp.
- Default page size: 50 messages. Backed by a database index on (`ConversationId`, `SentAt`, `Id`).
- Thread replies use the same strategy.

## 6. Design

Dark, neutral palette with a single accent color.

| Role | Color |
|---|---|
| Background | `#0F1115` |
| Surface (sidebar, cards) | `#171A21` |
| Elevated (inputs, hover) | `#1F232B` |
| Border | `#2A2F38` |
| Text primary | `#E6E8EB` |
| Text secondary | `#9AA1AC` |
| Accent | `#6C7BFF` (soft indigo) |
| Success / Warning / Error | `#3FB950` / `#D29922` / `#F85149` |

- **Logo**: `#Chat`, created from scratch by me. The `#` reads as "sharp", so the logo spells SharpChat.
  - Font: **JetBrains Mono**.
  - Colors: `Chat` in text primary (`#E6E8EB`), `#` in the **accent color**, so the pun stands out. If the accent changes, the `#` follows it.
  - Background: dark background (`#0F1115`).
  - **Compact version**: only the `#`, used as favicon and app icon, where the full logo would be unreadable.
- The UI uses JetBrains Mono for code blocks and inline code as well, keeping the logo and the code visually connected.
- Code blocks use a dark syntax theme that matches the background.
- Colors are defined once as CSS variables, so changing the accent is a one-line change.

## 7. Domain (initial concepts)

| Entity | Notes |
|---|---|
| `User` | Managed by ASP.NET Core Identity |
| `Conversation` | Name, created by, created at |
| `ConversationMember` | User + Conversation + role (`Owner`, `Admin`, `Member`) + delivered/read positions (`LastDeliveredSeq`, `LastReadSeq`) |
| `Message` | Id (generated by the client), conversation, sender, content (raw markdown), sent at, `Seq` (order in the conversation), `ChangeSeq` (sync position), `ParentMessageId` (null for main messages, set for thread replies), `ReplyCount` / `LastReplyAt` / `LastReplySeq` (on thread parents), `EditedAt`, `DeletedAt` |

Threads have one level only: a reply cannot have its own thread.

**Membership rules** (enforced in the `Conversation` aggregate):
- Role hierarchy: `Owner > Admin > Member`. The creator is the `Owner`.
- `Owner` and `Admin` add members. Members are added by **username**.
- A member can only remove members with a **lower** role. Anyone except the `Owner` can leave.
- The `Owner` can't leave or be removed. Ownership transfer is not supported yet.
- Only the `Owner` changes roles (`Admin` ↔ `Member`). The `Owner` role can't be assigned.
- Non-members get **404**, so they can't tell whether a conversation exists. Members without permission get **403**.

**Editing and deletion:**
- **Edit**: only the author can edit. The message shows an "edited" label. No edit history is kept.
- **Delete**: the author can delete their own messages; `Owner` and `Admin` can delete any message in the conversation.
- **Soft delete**: the row stays in the database with `DeletedAt` set and the content cleared. The UI shows "message deleted". This keeps threads and history consistent.
- No time limit for editing or deleting.

Details (fields, constraints, indexes) will be defined when modeling starts.

## 8. Rules for the three priorities

### Reliability
- **Persist before broadcast**: save the message, then send it to the group, then acknowledge the sender.
- **Client generates the message Id (GUID)** so retries don't create duplicates (idempotency).
- **Sync on reconnect**: client requests every message after the last one it received, plus edits and deletions that happened while it was offline.
- Message status: sent → delivered → read.

### Security
- Never trust a `userId` sent by the client. Always read it from the JWT claims.
- Every Hub method and endpoint checks that the user is a **member of the conversation**.
- Edit and delete check **authorship and role** on the server (see section 7), never on the client only.
- JWT is sent to SignalR via `access_token` query string (browsers can't set headers on WebSockets). Must be configured explicitly. Accepted **only on `/hubs/*`**. Since URLs can end up in logs, request logging stays at `Warning` for `Microsoft.AspNetCore`.
- SignalR **stateful reconnect stays disabled** (it was the attack surface of CVE-2026-70159). Keep Microsoft packages and the runtime on the latest patch.
- Rate limiting (built-in ASP.NET Core middleware) and message size limits.
- HTTPS / WSS only.
- Passwords hashed by ASP.NET Core Identity (never custom hashing).
- **Tokens in the browser**: the refresh token lives only in an `HttpOnly`, `Secure`, `SameSite=Strict` cookie; the access token lives only in memory (never `localStorage`).
- **Markdown is untrusted input**: rendered only through `rehype-sanitize`, raw HTML in messages is never executed (XSS protection).

### Low latency
- WebSockets transport for SignalR.
- Short send path: validate → save → broadcast.
- Markdown and highlighting happen on the client, so they add no server time.
- Optional later: MessagePack protocol for smaller payloads.

## 9. Roadmap (phases)

Tests are written along each phase, not only at the end.

| Phase | Content | Showable result |
|---|---|---|
| 0 | Solution structure, Docker Compose (API + Postgres + Redis), health check | Project runs with one command |
| 1 | Register, login, JWT + refresh token | Authenticated API |
| 2 | Conversations and members (REST) | Create groups, add/remove people |
| 3 | SignalR Hub: send/receive messages, persistence, history | **Working chat (MVP)** |
| 4 | React frontend with dark theme, markdown and code blocks | Usable dev chat in the browser |
| 5 | Reliability: idempotency, reconnect sync, delivered/read status | Robust chat |
| 6 | Message threads, editing and deletion (backend + frontend) | Full dev chat concept |
| 7 | Redis backplane, presence, typing indicator, rate limiting | Scales to multiple instances |
| 8 | Observability stack, test coverage review, README with architecture diagram | Portfolio ready |
| Later | Deploy | Live demo |

### Git workflow

| Item | Rule |
|---|---|
| `main` | Stable releases only. The first merge into `main` is the first stable version of the app |
| `develop` | Integration branch. Every finished phase is merged here |
| Phase branches | `phase-N-<name>` (e.g. `phase-1-auth`), created from `develop`, deleted after the merge (on GitHub). Non-phase work uses `chore-<name>` |
| Merges | Done **only by me, on GitHub** (never locally). Claude tells me when a branch is ready to merge into `develop`. Merge commits keep each phase visible as a group in the history |
| Commits | English, imperative summary line, a body listing what changed. A phase is merged only when its tests pass |
| Merge text | Every merge (pull request) uses the same template, in English: title `Phase N: <name> (<key points>)`, then `## Purpose` (why, which phase it closes), `## Changes` (grouped by Backend / Frontend / Docs and repo), `## How to test` (commands + manual check) and `## Notes` (limitations, optional). Claude writes it when the branch is ready |
| Push | Done **only by me**. Claude never pushes; it tells me when a push is recommended, at the strategic points below |

**Strategic push points** (Claude notifies me, I push):
1. **Phase or chore finished**: push the branch; Claude says it is ready to merge into `develop` on GitHub. Before starting the next branch, I pull `develop`.
2. **End of a session with a phase in progress**: push the phase branch as a backup, so unfinished work is never only local.
3. **Before a risky change** (large refactor, migration rework): push the current branch first, so there is a known-good point on GitHub.

## 10. Open decisions

New ideas go here before becoming decisions.

## 11. Decision log

| Date | Decision | Reason |
|---|---|---|
| 2026-10-18 | Group chats | 1:1 is a special case of a group; one model covers both |
| 2026-10-18 | React frontend | More marketable, already familiar with the ecosystem |
| 2026-10-18 | No deploy for now | Focus on building; Docker keeps deploy easy later |
| 2026-10-18 | Modular monolith + Clean Architecture | Right size for the problem, easy to explain |
| 2026-10-18 | Grafana + Prometheus + Loki + Jaeger | Classic, widely used observability stack; OpenTelemetry keeps the app vendor-neutral |
| 2026-10-18 | Dev chat concept (markdown, code blocks, threads) | Avoids a generic chat; recruiters are developers and relate to it |
| 2026-10-18 | Dark neutral palette | Fits a developer tool; single accent keeps it neutral |
| 2026-10-18 | Store raw markdown, render on client with sanitizer | Keeps the server fast and simple, and prevents XSS |
| 2026-10-18 | Name: SharpChat (repo `sharpchat`, namespaces `SharpChat.*`) | References C# without using `#`, which breaks repos, namespaces and URLs |
| 2026-10-18 | Logo `#Chat` (monospace, white on dark) | `#` = sharp, also evokes chat channels (`#general`); fits the Dev chat concept |
| 2026-10-18 | Logo refined: JetBrains Mono, `#` in accent color, compact `#` icon | Modern dev font, highlights the pun, readable at favicon size |
| 2026-10-18 | Accent color: soft indigo `#6C7BFF` | Fits a developer tool, stands out on the dark palette |
| 2026-10-18 | Attachments out of scope | Keeps the focus on realtime messaging; avoids file storage and upload security for now |
| 2026-10-18 | Group limit: 100 members (configurable) | Enough for teams and study groups; caps the fan-out cost of each broadcast |
| 2026-10-18 | Cursor-based pagination for history | Stable with new messages arriving, fast with an index, standard in chat apps |
| 2026-10-18 | Message editing and deletion (soft delete) | Expected in any modern chat; soft delete keeps threads and history consistent |
| 2026-10-18 | HTTPS redirection/HSTS only outside Development; TLS terminated by a reverse proxy at deploy | Keeps local Docker and tests simple; the HTTPS-only rule still holds in production |
| 2026-10-18 | Git flow: phase branches → `develop`; `main` only for stable releases; push confirmed manually | Keeps `main` always stable and the history organized by phase, which reads well in a portfolio |
| 2026-10-18 | Auth required by default (fallback policy); public endpoints opt out with `[AllowAnonymous]` | A forgotten attribute fails closed instead of exposing an endpoint |
| 2026-10-18 | Refresh tokens: random 64 bytes, stored as SHA-256 hash, rotated on every use, grouped in families; reuse of a rotated token revokes the family | Limits the damage of a stolen token and detects theft; a DB leak doesn't expose usable tokens |
| 2026-10-18 | Access token 15 min, refresh token 14 days | Short-lived access keeps revocation delay small; refresh keeps users logged in |
| 2026-10-18 | Password policy: min 8 chars, upper + lower + digit; lockout after 5 failures for 5 min | Reasonable baseline; brute force is also covered by rate limiting in Phase 7 |
| 2026-10-18 | Identity without roles (`IdentityUserContext`) | Roles in SharpChat are per conversation (`ConversationMember.Role`), not global |
| 2026-10-18 | JWT signing key only from user-secrets / `.env`; startup fails without it | The key is never committed; misconfiguration is caught at startup |
| 2026-10-18 | Backend in `backend/`, frontend in `web/`; Docker Compose and docs at the root | Clear separation of the two apps; the root holds only what is shared |
| 2026-10-18 | `Result`/`Error` live in Domain; aggregates return results instead of throwing | Business rule violations are expected outcomes, not exceptions; same type flows from Domain to the API |
| 2026-10-18 | Commands load the aggregate (`IConversationRepository`); reads go through `IConversationQueries` returning DTOs | Rules stay in the domain; reads stay fast and can join usernames without loading aggregates |
| 2026-10-18 | Conversation row changes on every membership change (`UpdatedAt`) and is guarded by `xmin` | Concurrent adds can't exceed the member limit; the loser gets `409 concurrent_update` |
| 2026-10-18 | Membership rules (see section 7) | Simple hierarchy, easy to explain; protects the owner |
| 2026-10-18 | Enums serialized as strings in the API, roles stored as strings in the database | Readable payloads and data; adding a role doesn't shift numeric values |
| 2026-10-18 | Pushes only by me; Claude notifies at strategic points (phase merged, session end with a phase in progress, before risky changes) | I keep control of what reaches GitHub, without having to think about when to push |
| 2026-10-18 | No `/api` prefix on routes (`/auth`, `/users`, `/conversations`) | Shorter, cleaner URLs. If the frontend is ever served from the same origin, its routes must not collide with these (or a reverse proxy adds a prefix) |
| 2026-10-18 | No comments in code | Code should explain itself; the reasoning lives in this file, the implementation plan and commit messages |
| 2026-10-19 | Messages delivered with `Clients.Users(memberIds)`, members read from the database on every send (no SignalR groups) | Delivery always follows the current membership, with no group sync and no per-connection state to move to Redis. Costs one backplane publish per member, which the 100-member cap keeps bounded |
| 2026-10-19 | Hub methods return `{ data, error }` with the same error codes as REST | The client handles both APIs the same way; no parsing of exception messages |
| 2026-10-19 | History cursor is opaque (base64url of `SentAt` ticks + `Id`); pages come newest first, items oldest first | Clients don't depend on the format; the newest messages load first, ready to render top-down |
| 2026-10-19 | `SentAt` set by the server and truncated to microseconds | Server time can't be faked by clients; matches Postgres precision, so values seen in realtime events are valid cursors |
| 2026-10-19 | Microsoft packages on 10.0.13 (CVE-2026-70159, SignalR DoS) | Patched version; the Docker image (`aspnet:10.0`) already runs 10.0.13 |
| 2026-10-19 | Frontend folder is `frontend/` (replaces `web/`) | Mirrors `backend/`, so the two apps read as a pair |
| 2026-10-20 | Refresh token only in an `HttpOnly`, `Secure`, `SameSite=Strict` cookie (`__Secure-sharpchat-refresh`, path `/auth`); access token kept in memory only. `login`/`refresh` return just the access token; `refresh`/`logout` read only the cookie; logout and a failed refresh clear it | JavaScript (and so an XSS) can never read the long-lived token. `SameSite=Strict` blocks cross-site use of `refresh`/`logout`. The cookie is an HTTP concern, so it lives in the Api layer and the Application layer is unchanged |
| 2026-10-20 | The browser calls the API under `/api` on the frontend's own origin; a proxy (Vite in dev, reverse proxy in Compose) strips the prefix and rewrites the cookie path to `/api/auth` | Same origin, so no CORS and the strict cookie works. The backend stays prefix-free, and frontend routes can't collide with API routes |
| 2026-10-20 | Token refreshes are serialized across browser tabs with the Web Locks API | With rotation and reuse detection, two tabs refreshing with the same cookie at once would look like token theft and revoke the session |
| 2026-10-20 | Frontend: React Router (data router), CSS Modules over the token variables, JetBrains Mono self-hosted via `@fontsource` | No CSS framework needed for one dark theme; no request to an external font CDN |
| 2026-10-20 | Server state in the frontend through TanStack Query | Caching, refetch and invalidation after mutations out of the box; `useInfiniteQuery` fits the cursor-paginated history |
| 2026-10-20 | Swagger UI at `/swagger`, Development only, over the built-in OpenAPI document with a Bearer scheme | Lets the API be tried by hand without the frontend; not exposed in production |
| 2026-10-20 | Raw HTML in messages is shown as literal text; markdown images are rendered as links | Developers talk about HTML, so tags shouldn't silently disappear; images as links means a message can't load remote content (tracking pixels) |
| 2026-10-20 | Shiki output converted hast → JSX (no `innerHTML`), JS regex engine, loaded lazily on the first code block | No HTML string is ever injected; no WebAssembly download; the main bundle stays small |
| 2026-10-20 | Frontend served by nginx in Compose (port 3000) with a strict Content-Security-Policy (`'self'` only, no inline scripts or styles) | Defense in depth: even if sanitization failed, injected scripts couldn't run or call other origins |
| 2026-10-20 | Merges into `develop`/`main` happen only on GitHub, done by me; Claude never merges locally and tells me when a branch is ready | Merges stay visible and reviewable on GitHub, and nothing reaches `develop` without my action |
| 2026-10-20 | Message order and sync use a per-conversation sequence: `Seq` (creation order) and `ChangeSeq` (sync position), assigned under `SELECT ... FOR UPDATE` on the conversation row | Gap-free numbers visible in commit order, so "everything after N" never skips a message (timestamps can't guarantee that); also serializes resends of the same id. No counter column, so the row's `xmin` (membership concurrency) is untouched |
| 2026-10-20 | Delivered/read are cumulative positions per member (`LastDeliveredSeq`, `LastReadSeq`), only moving forward, read implies delivered; updated with set-based SQL outside the aggregate | One row update per ack instead of one row per message per member; frequent writes don't conflict with membership changes |
| 2026-10-20 | `ReceiptUpdated` is sent only to the authors of the messages whose status changed, plus the reader | Avoids an N² fan-out in 100-member groups (every ack to every member) |
| 2026-10-20 | Group status: delivered/read counted over all other members, shown as terminal-style tags (`[delivered k/N]`, `[read k/N]`, accent when everyone read) | Informative in groups and fits the dev-chat look, instead of copying WhatsApp's check marks |
| 2026-10-20 | Outbox kept in memory only; unacked messages are resent with the same id on every reconnect | Covers dropped connections; a page reload while offline loses the unsent text, an accepted trade-off for simplicity and to keep message text out of browser storage |
| 2026-10-20 | Existing history is marked as read for current members by the sequence migration | Avoids a wall of stale unread badges after the upgrade |
| 2026-10-20 | Standard merge text: Purpose, Changes, How to test, Notes | Every merge reads the same way on GitHub, so the history doubles as a changelog |
| 2026-10-20 | Threads are Slack-style: replies live only in the thread panel; the parent keeps `ReplyCount`, `LastReplyAt`, `LastReplySeq` and gets a new `ChangeSeq` on every reply | The main timeline stays readable; counters are server-authoritative and reach offline clients through the normal sync |
| 2026-10-20 | Replies don't count as unread; one read position per member covers the whole conversation; the parent shows "new" while it has replies after your read position | Long side discussions don't flood the badge of people not following them, with no extra table or events |
| 2026-10-20 | Edit and delete go through the hub (`EditMessage`, `DeleteMessage`) and each takes a new `ChangeSeq` under the conversation lock | Same path, ordering and sync guarantees as sending |
| 2026-10-20 | Deleted messages have their content erased on the server; deleting twice is a silent no-op | Deleted text never travels again; retries are safe |
| 2026-10-20 | The client ignores updates to messages older than its loaded pages (they come with the page when it loads) and keeps its own sync position per conversation | An edit of an old message can't land in the newest page, and updates to unloaded parents don't trigger repeated syncs |
| 2026-10-20 | SignalR Redis backplane; clients use WebSockets only with `skipNegotiation`, so the load balancer needs no sticky sessions | Any instance can serve any client; no affinity to configure or lose on failover |
| 2026-10-20 | Presence: per user, a Redis sorted set of connections with an expiry renewed by the owning instance every 20 s; Lua scripts decide online/offline transitions; the client waits 5 s before showing offline | Several tabs and instances agree on one state; a crashed instance's connections expire on their own; a page reload doesn't flicker |
| 2026-10-20 | Presence events only go to users who share a conversation; `GetOnlineMembers` is scoped to a conversation the caller belongs to | Strangers can't watch who is online |
| 2026-10-20 | Typing indicator: hub `Typing` per conversation or thread, never persisted, client sends at most every 3 s and shows it for 5 s | Cheap, and stale indicators clear themselves |
| 2026-10-20 | Rate limiting with ASP.NET Core's middleware and Redis counters (`RedisRateLimiting`): auth per IP, the rest per user; a hub filter for message methods and typing | Limits hold across instances (in-memory counters would double with two instances); excess typing is dropped instead of failing |
| 2026-10-20 | Forwarded headers trusted from private networks only | Rate limits see the real client IP behind nginx; a client on the internet can't fake it. Locally the published API port is still reachable from the host |
| 2026-10-20 | Load balancer: the frontend's nginx with an upstream (`least_conn`, shared `zone`) over `api-1` and `api-2` | No extra service; the shared zone makes all nginx workers balance together |
| 2026-10-20 | Both instances run migrations on startup in Development; EF Core's migration lock makes the second one wait | Checked with two containers against an empty database: one applies, the other finds it up to date |

| 2026-10-21 | Telemetry leaves the API through one OTLP endpoint to an OpenTelemetry Collector, which sends traces to Jaeger, metrics to Prometheus (its OTLP receiver) and logs to Loki (its OTLP endpoint) | The API knows a single destination; swapping a backend is a Collector config change. No scrape targets to list per instance |
| 2026-10-21 | Logs stay on Serilog (console + OTLP sink); OpenTelemetry is only registered when `OTEL_EXPORTER_OTLP_ENDPOINT` is set | Each log line carries `trace_id`/`span_id`, so Grafana jumps from a log to its trace. Tests and runs without the stack pay nothing |
| 2026-10-21 | Observability services live in the `observability` Compose profile; the APIs always point at the Collector | `docker compose up` stays light; `--profile observability` adds the stack without editing env vars. Without the Collector the exporters just drop data |
| 2026-10-21 | Custom metrics in a `SharpChat` meter: messages sent (message/reply), send duration with outcome (created/duplicate/rejected), edits/deletes, presence transitions, hub calls rate limited. Hub connections, HTTP and REST rate-limit metrics come from ASP.NET Core's built-in meters | Measures what only the app knows (idempotent retries, presence) and reuses what the framework already emits |
| 2026-10-21 | Grafana dashboards (API, Messaging, Runtime) are generated JSON, provisioned read-only, with anonymous admin access on port 3001 | Ready on first start with no login for a local demo; not a production setup |
| 2026-10-21 | `OnConnectedAsync` no longer uses the connection's abort token and undoes the registry entry if presence fails | SignalR runs `OnConnectedAsync` after the handshake and skips `OnDisconnectedAsync` when it throws; a tab closed in that window stayed online forever (found by the presence metric test) |
| 2026-10-21 | nginx re-resolves `api-1`/`api-2` through Docker's DNS (`resolve` on the upstream servers) | Recreating an API container changed its IP and nginx kept the old one until restarted |
