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
├── SharpChat.sln
├── docker-compose.yml
├── DECISIONS.md
├── src/
│   ├── SharpChat.Domain/          # Entities and business rules
│   ├── SharpChat.Application/     # Use cases, interfaces, DTOs, validation
│   ├── SharpChat.Infrastructure/  # EF Core, repositories, Redis, JWT, Identity
│   └── SharpChat.Api/             # Controllers, SignalR Hubs, middleware, Program.cs
├── tests/
│   ├── SharpChat.UnitTests/
│   └── SharpChat.IntegrationTests/
└── web/                         # React + TypeScript frontend
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
| Observability | OpenTelemetry (instrumentation) + Prometheus (metrics) + Loki (logs, via Serilog) + Jaeger (traces) + Grafana (dashboards) + health checks |

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
| `ConversationMember` | User + Conversation + role (`Owner`, `Admin`, `Member`) + last read message |
| `Message` | Id (generated by the client), conversation, sender, content (raw markdown), sent at, `ParentMessageId` (null for main messages, set for thread replies), `EditedAt`, `DeletedAt` |

Threads have one level only: a reply cannot have its own thread.

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
- JWT is sent to SignalR via `access_token` query string (browsers can't set headers on WebSockets). Must be configured explicitly.
- Rate limiting (built-in ASP.NET Core middleware) and message size limits.
- HTTPS / WSS only.
- Passwords hashed by ASP.NET Core Identity (never custom hashing).
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
| Phase branches | `phase-N-<name>` (e.g. `phase-1-auth`), created from `develop`, deleted after the merge |
| Merges | `--no-ff`, so each phase stays visible as a group in the history |
| Commits | English, imperative summary line, a body listing what changed. A phase is merged only when its tests pass |
| Push | To `origin` (GitHub) when it makes sense, e.g. after a phase is merged into `develop`. Always confirmed by me first |

## 10. Open decisions

- **Refresh token storage in the browser** (decide in Phase 4): the API currently returns it in the JSON body. Option: move it to an `HttpOnly`, `Secure`, `SameSite=Strict` cookie so JavaScript (and XSS) can't read it.

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
