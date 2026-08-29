# emoji-waterfall.lol — Design

Date: 2026-08-29

## Purpose

A public website that fetches the full emoji set from a third-party API and
animates the emoji falling down the screen like a waterfall, cycling
continuously.

The site is the excuse. The actual goal is hands-on practice with C#/ASP.NET
Core, React, AWS (EC2, SSM, budgets, DNS/TLS), and the glue between them.

## Working agreement

The developer writes all production code. Claude's role is to scaffold, hand
over the exact commands to run, explain what each step is doing and why, and
review code on request. Claude does not write code in `api/`, `web/`, or
`deploy/` without asking first.

Explanation is a deliverable, not a bonus: each phase should leave the
developer able to say what every moving part does.

## Non-goals

- No database. The emoji set is static enough to live in memory.
- No user accounts, no persistence, no analytics.
- No CI/CD in v1. Deploys are a script run by hand.
- No infrastructure-as-code in v1. Console for the first launch, CLI after.

## Architecture

A single EC2 instance does everything.

    browser
      |
      | https://emoji-waterfall.lol
      v
    nginx  :80 / :443          (TLS termination, static files, reverse proxy)
      |-- /            -> /var/www/emoji-waterfall  (React production build)
      |-- /api/*       -> http://127.0.0.1:5000     (Kestrel)
                                  |
                                  | HttpClient, on startup + periodic refresh
                                  v
                          https://emoji-api.com/emojis?access_key=...

Kestrel binds to loopback only. It is unreachable from the internet except
through nginx.

### Infrastructure

| Item | Choice |
|---|---|
| Region | us-east-1 |
| Instance | t4g.nano (ARM64 Graviton, 2 vCPU, 0.5 GB) |
| AMI | Amazon Linux 2023, arm64 |
| Storage | 8 GB gp3 root volume |
| Swap | 1 GB swapfile (0.5 GB RAM is tight for .NET + nginx) |
| Address | Elastic IP, always associated |
| Security group | 22 from developer IP only; 80 and 443 from anywhere |
| DNS | Namecheap A record -> Elastic IP (no Route53) |
| TLS | Let's Encrypt via certbot, auto-renew timer |
| Secret | emoji-api.com access key in SSM Parameter Store (SecureString) |

### Cost

Accepted running cost is roughly $7.40/month:

| Item | Monthly |
|---|---|
| t4g.nano on-demand | ~$3.07 |
| Public IPv4 | ~$3.65 |
| 8 GB gp3 | ~$0.64 |

An AWS Budget is set at **$10/month** with email alerts. AWS provides no hard
spend cap; the budget is an alarm, not a wall. Prices are confirmed against
the AWS calculator before launch.

## Components

### 1. API — `api/EmojiWaterfall.Api`

ASP.NET Core minimal API, .NET 10, published as linux-arm64.

Endpoints:

- `GET /api/health` — returns a static OK payload. Exists to verify Kestrel,
  systemd, and the nginx proxy independently of any upstream dependency.
- `GET /api/emojis` — returns the cached emoji collection.

Response shape for `/api/emojis`:

    [ { "character": "😀", "name": "grinning face", "group": "smileys-emotion" } ]

Upstream handling: fetch `https://emoji-api.com/emojis?access_key=...` once at
startup via a typed `HttpClient`, project the upstream model down to the slim
DTO above, and hold the result in memory. Refresh on a timer (24h is ample).

Rationale: the emoji set is effectively static, so per-request proxying would
mean thousands of pointless round trips; caching also keeps the access key on
the server, which is the reason a keyed source was chosen over a public CDN
file.

Configuration: access key read from `IConfiguration`. `dotnet user-secrets`
locally; on EC2, pulled from SSM Parameter Store and supplied to the process
as an environment variable by the systemd unit.

### 2. Frontend — `web/`

Vite + React + TypeScript.

Fetches `/api/emojis` once on mount, then animates. In development, Vite's
proxy forwards `/api` to the local API, so the app is same-origin in every
environment and CORS never enters the picture.

Animation: **CSS keyframe animations** for v1 — emoji arranged in columns,
each column translating downward on a loop with staggered delays, durations,
and starting offsets so the columns desynchronise. Declarative, GPU
composited, and very little code. A `requestAnimationFrame` loop is the
documented upgrade path if per-emoji control becomes necessary.

### 3. Deploy — `deploy/`

- `nginx.conf` — server block: static root, `/api` proxy pass, TLS config
- `emoji-api.service` — systemd unit for Kestrel
- `deploy.sh` — build both projects, copy artifacts to the instance,
  restart the service

## Repository layout

    emoji-waterfall/
      api/      EmojiWaterfall.Api, EmojiWaterfall.Api.Tests
      web/      Vite React app
      deploy/   nginx.conf, emoji-api.service, deploy.sh
      docs/     specs and notes
      README.md

Single git repository, hosted on GitHub.

## Error handling

- **Upstream fetch fails at startup**: the API still starts and serves
  `/api/health`. `/api/emojis` returns 503 until a fetch succeeds.
- **Upstream fetch fails on refresh**: keep serving the previously cached
  set. A stale emoji list is strictly better than an empty screen.
- **Frontend fetch fails**: render a visible error state rather than a blank
  page, and retry with backoff.
- **Instance reboot**: systemd unit is enabled, so Kestrel restarts
  automatically; nginx likewise.

## Testing

- **API**: xUnit. The upstream client sits behind an interface so the
  projection and caching logic can be tested with no network access.
- **Frontend**: Vitest, optional at this scale.
- **Infrastructure**: verified by hand at each phase — curl the health
  endpoint locally on the box, then through nginx, then from the public
  internet.

## Build phases

| Phase | Goal | Done when |
|---|---|---|
| 0 | Repo scaffold, git init, .gitignore | `git log` shows an initial commit |
| 1 | Hello-world API + bare React page, locally | Browser at localhost renders data from `/api/health` |
| 2 | EC2, nginx, systemd, budget alert | Thin slice loads over http at the Elastic IP |
| 3 | Namecheap DNS + certbot | `https://emoji-waterfall.lol` loads with a valid cert |
| 4 | Real emoji fetch, caching, SSM secret | `/api/emojis` returns the full set in production |
| 5 | The waterfall animation | Emoji fall and cycle continuously |
| 6 | Optional: GitHub Actions deploy, or S3+CloudFront frontend | — |

Phases 0-1 are local only and cost nothing. No AWS resource is created
before Phase 2.

## Deferred decisions

- Whether to leave the instance running 24/7 long-term, or stop it when idle
  (revisit after Phase 5, once the site is worth looking at).
- Whether to migrate the frontend to S3 + CloudFront (Phase 6).
- Whether to add GitHub Actions deployment (Phase 6).
