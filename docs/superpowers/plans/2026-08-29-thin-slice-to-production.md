# emoji-waterfall.lol — Plan 1: Thin Slice to Production

> **For the developer:** This plan is written to be executed *by you*, by hand.
> Claude supplies commands, contracts, and explanation; you write every line of
> code in `api/`, `web/`, and `deploy/`. Steps use checkbox (`- [ ]`) syntax so
> you can track position across sessions. Ask Claude to review anything you
> write; stop and ask if a verification step fails.

**Goal:** Get a trivial C# API and a bare React page live at
`https://emoji-waterfall.lol`, served from a single EC2 instance, with no emoji
logic anywhere.

**Architecture:** nginx terminates TLS, serves the React production build as
static files, and reverse-proxies `/api/*` to a Kestrel process bound to
loopback and managed by systemd. Deploys are a shell script run by hand.

**Tech Stack:** .NET 10 minimal API, xUnit, Vite + React + TypeScript, nginx,
systemd, Amazon Linux 2023 on ARM64, Let's Encrypt via certbot.

**Spec:** `docs/superpowers/specs/2026-08-29-emoji-waterfall-design.md`

## Why this order

Every phase before 2 costs nothing and runs locally. Phase 2 is where AWS starts
billing, and it deliberately deploys an application with no dependencies beyond
"does it return 200" — so when something breaks between the browser and Kestrel,
you are debugging exactly one thing: the plumbing. Adding emoji fetching before
the plumbing works would mean debugging two unknowns at once.

## Global Constraints

- **The developer writes all production code.** Claude does not write files in
  `api/`, `web/`, or `deploy/` without asking first.
- .NET SDK 10.0.400; Node 24.13.0; npm 11.6.2 (verified present)
- AWS region: `us-east-1`
- Instance: `t4g.nano`, Amazon Linux 2023, **arm64** — every artifact must be
  built for `linux-arm64`, not `linux-x64`
- Kestrel binds `127.0.0.1:5000` only — never `0.0.0.0`
- `<ServerGarbageCollection>false</ServerGarbageCollection>` — mandatory on a
  512 MB instance
- AWS Budget set to $10/month with email alerts, created *before* any billable
  resource
- Git branch is `master`; remote is `origin` → `frogsupport/emoji-waterfall`

## File Structure

| Path | Responsibility |
|---|---|
| `api/EmojiWaterfall.sln` | Solution tying the two .NET projects together |
| `api/EmojiWaterfall.Api/EmojiWaterfall.Api.csproj` | API project + GC config |
| `api/EmojiWaterfall.Api/Program.cs` | Minimal API host and endpoint definitions |
| `api/EmojiWaterfall.Api.Tests/` | xUnit integration tests via `WebApplicationFactory` |
| `web/vite.config.ts` | Dev server + `/api` proxy to local Kestrel |
| `web/src/App.tsx` | Fetches `/api/health`, renders the result |
| `deploy/emoji-api.service` | systemd unit for Kestrel |
| `deploy/nginx.conf` | Server block: static root + `/api` reverse proxy |
| `deploy/deploy.sh` | Build both projects, ship artifacts, restart service |

---

## Phase 1 — Local thin slice

### Task 1: API project scaffold and health endpoint

**Files:**
- Create: `api/EmojiWaterfall.sln`
- Create: `api/EmojiWaterfall.Api/EmojiWaterfall.Api.csproj`
- Create: `api/EmojiWaterfall.Api/Program.cs`

**Produces:** `GET /api/health` → `200 OK`, body `{"status":"ok"}`,
`Content-Type: application/json`. Task 2 tests this contract; Task 3's frontend
and Task 9's nginx config both depend on the exact path `/api/health`.

- [ ] **Step 1: Create the solution file**

```bash
cd api
dotnet new sln -n EmojiWaterfall
```

*Why:* a solution is just a list of projects. You do not strictly need one for a
single project, but you are adding a test project in Task 2, and `dotnet build`
and `dotnet test` at the solution level then operate on both at once.

- [ ] **Step 2: Create the API project**

```bash
dotnet new web -n EmojiWaterfall.Api
dotnet sln add EmojiWaterfall.Api/EmojiWaterfall.Api.csproj
```

*Why the `web` template and not `webapi`:* `web` is the empty minimal-API
template — a `Program.cs`, a csproj, nothing else. `webapi` adds controllers,
OpenAPI wiring, and a sample weather endpoint you would spend your first ten
minutes deleting. You want to see every line that exists.

- [ ] **Step 3: Configure the csproj for a 512 MB instance**

Open `api/EmojiWaterfall.Api/EmojiWaterfall.Api.csproj` and add two properties
inside the existing `<PropertyGroup>`:

- `ServerGarbageCollection` → `false`
- `InvariantGlobalization` → `true`

*Why:* ASP.NET Core selects Server GC when it detects multiple cores, and
`t4g.nano` reports 2 vCPUs. Server GC keeps per-core heaps and delays collection
because it assumes RAM is abundant — the wrong trade at 512 MB. Workstation GC
collects sooner and holds far less. `InvariantGlobalization` drops the ICU
dependency (~30 MB of locale data) you have no use for, shrinking both the
artifact and startup time.

- [ ] **Step 4: Write the health endpoint**

In `Program.cs`, define `GET /api/health` returning the JSON object
`{"status":"ok"}` with a 200 status.

*This is yours to write.* Things to work out as you do: minimal APIs map routes
with `app.MapGet(pattern, handler)`; returning an object from the handler gets
serialized to JSON automatically with the right content type. Keep the `/api`
prefix in the route — nginx will proxy on that prefix in Task 9, and matching it
here means the path is identical in dev and production, so nothing needs
rewriting.

- [ ] **Step 5: Run it**

```bash
dotnet run --project EmojiWaterfall.Api
```

Note the port it prints. The template picks one from
`Properties/launchSettings.json`.

- [ ] **Step 6: Pin the port to 5000**

Edit `api/EmojiWaterfall.Api/Properties/launchSettings.json` so the HTTP profile
uses `http://localhost:5000`.

*Why:* Task 3's Vite proxy and Task 8's systemd unit both assume 5000. One port
everywhere is one less thing to get wrong.

- [ ] **Step 7: Verify**

```bash
curl -i http://localhost:5000/api/health
```

Expected: `HTTP/1.1 200 OK`, `Content-Type: application/json`, body
`{"status":"ok"}`.

If you get 404, the route pattern does not match. If the body comes back quoted
like `"{\"status\":\"ok\"}"`, you returned a string rather than an object.

- [ ] **Step 8: Commit**

```bash
cd ..
git add api/
git commit -m "Add API project with health endpoint"
git push
```

---

### Task 2: API integration test project

**Files:**
- Create: `api/EmojiWaterfall.Api.Tests/EmojiWaterfall.Api.Tests.csproj`
- Create: `api/EmojiWaterfall.Api.Tests/HealthEndpointTests.cs`
- Modify: `api/EmojiWaterfall.Api/Program.cs` (make `Program` visible to tests)

**Consumes:** `GET /api/health` from Task 1.
**Produces:** a working `dotnet test` at the solution level. Every later task
that touches the API re-runs this.

- [ ] **Step 1: Create the test project and wire it up**

```bash
cd api
dotnet new xunit -n EmojiWaterfall.Api.Tests
dotnet sln add EmojiWaterfall.Api.Tests/EmojiWaterfall.Api.Tests.csproj
dotnet add EmojiWaterfall.Api.Tests reference EmojiWaterfall.Api
dotnet add EmojiWaterfall.Api.Tests package Microsoft.AspNetCore.Mvc.Testing
```

*Why `Mvc.Testing`:* it provides `WebApplicationFactory<T>`, which boots your
entire app in-process — real routing, real DI, real serialization — and hands
you an `HttpClient` wired directly to it with no network involved. You test the
actual HTTP contract without starting a server or binding a port. This is the
right default for API testing in ASP.NET Core; mocking at the handler level
tests less and breaks more.

- [ ] **Step 2: Make `Program` accessible to the test project**

Minimal APIs generate an implicit `Program` class that is `internal`, so
`WebApplicationFactory<Program>` cannot see it from another assembly. Fix it by
adding a `public partial class Program { }` declaration at the bottom of
`Program.cs`.

*Why this works:* the compiler-generated entry point class is `partial`, so your
declaration merges with it and widens its accessibility. The alternative is an
`InternalsVisibleTo` attribute; the partial-class trick is the conventional one
and needs no attribute plumbing.

- [ ] **Step 3: Write the test**

In `HealthEndpointTests.cs`, write a test that:

1. Constructs a `WebApplicationFactory<Program>`
2. Calls `CreateClient()`
3. `GET`s `/api/health`
4. Asserts the status code is 200
5. Asserts the response body deserializes to an object whose `status` is `"ok"`

*Assert on the parsed body, not the raw string.* A string comparison against
`{"status":"ok"}` breaks the first time serializer settings change property
casing — a brittle test failing for a reason unrelated to behaviour.

- [ ] **Step 4: Run it**

```bash
dotnet test
```

It should **pass** immediately, because Task 1 already implemented the endpoint.
That is expected here, and it is the one place in this plan where the test does
not fail first — you are retrofitting a test onto working scaffold to establish
the harness. Every task from Phase 4 onward writes the test first.

To confirm the test is real rather than vacuously passing, temporarily change
the endpoint to return `"degraded"`, re-run, watch it fail, then change it back.
A test you have never seen fail is not yet evidence of anything.

- [ ] **Step 5: Commit**

```bash
cd ..
git add api/
git commit -m "Add integration test harness for health endpoint"
git push
```

---

### Task 3: React app scaffold and dev proxy

**Files:**
- Create: `web/` (Vite scaffold)
- Modify: `web/vite.config.ts`
- Modify: `web/src/App.tsx`

**Consumes:** `GET /api/health` from Task 1.
**Produces:** a dev server on port 5173 that renders API status.

- [ ] **Step 1: Scaffold**

```bash
npm create vite@latest web -- --template react-ts
cd web
npm install
```

The `--` matters: it stops npm consuming `--template` as its own flag and passes
it through to `create-vite`.

- [ ] **Step 2: Confirm the scaffold brought its own .gitignore**

```bash
cat .gitignore
```

Expected to contain `node_modules` and `dist`. This is the file you deliberately
chose over one large root ignore.

- [ ] **Step 3: Add the dev proxy**

In `vite.config.ts`, add a `server.proxy` entry mapping `/api` to
`http://localhost:5000`.

*Why:* the Vite dev server runs on 5173 and your API on 5000 — different
origins, so a direct browser fetch would be blocked by CORS. The proxy makes the
dev server forward `/api/*` to Kestrel server-side, so from the browser's point
of view everything is same-origin. In production nginx plays exactly this role.
Configuring it this way means **CORS never appears in this project**, and the
frontend code is identical in both environments.

- [ ] **Step 4: Fetch and render**

Replace the Vite demo content in `App.tsx` with a component that fetches
`/api/health` on mount and renders the returned status, plus a visible error
state if the request fails.

*This is yours to write.* Use a relative URL — `/api/health`, never
`http://localhost:5000/api/health` — or you defeat the proxy and hardcode an
address that will be wrong in production. Handle the error branch now rather
than later: the spec calls for a visible error state instead of a blank page,
and a page that renders nothing when the API is down is genuinely hard to debug
over SSH in Phase 2.

- [ ] **Step 5: Verify both halves together**

Two terminals:

```bash
# terminal 1
cd api && dotnet run --project EmojiWaterfall.Api

# terminal 2
cd web && npm run dev
```

Open `http://localhost:5173`. Expected: the page renders `ok`. Then stop the API
and reload — expected: your error state, not a blank page or an unhandled
promise rejection in the console.

- [ ] **Step 6: Commit**

```bash
cd ..
git add web/
git commit -m "Add React frontend with dev proxy and health check"
git push
```

---

### Task 4: Production build dry run

**Files:** none created — this task proves the build works before AWS is
involved.

- [ ] **Step 1: Publish the API for ARM64**

```bash
cd api
dotnet publish EmojiWaterfall.Api -c Release -r linux-arm64 --self-contained true -o ../publish/api
```

*Why `linux-arm64`:* `t4g` instances are AWS Graviton — ARM processors. An x64
build will not execute there, and the error you get is unhelpful.

*Why `--self-contained true`:* it bundles the .NET runtime into the output, so
the instance needs no .NET installed at all — no repository wrangling for a .NET
10 runtime on Amazon Linux 2023, no version drift between your machine and the
server. The cost is artifact size (roughly 70–90 MB rather than ~1 MB). On a
nano with an 8 GB disk that is a fine trade, and it removes an entire class of
"works locally, not on the server" failure.

- [ ] **Step 2: Build the frontend**

```bash
cd ../web
npm run build
```

- [ ] **Step 3: Verify both artifacts**

```bash
ls -la ../publish/api/EmojiWaterfall.Api
ls -la dist/
```

Expected: an `EmojiWaterfall.Api` executable (no `.exe` — it is a Linux binary),
and a `dist/` containing `index.html` plus a hashed `assets/` folder.

- [ ] **Step 4: Confirm build output is ignored**

```bash
cd ..
git status --short
```

Expected: **empty**. If `publish/` appears, your ignore rules have a gap — fix it
before committing anything. `publish/` is a new directory at the repo root that
neither subproject's ignore file anticipates, so it needs a rule in the root
`.gitignore`.

- [ ] **Step 5: Commit the ignore fix**

```bash
git add .gitignore
git commit -m "Ignore publish output"
git push
```

---

## Phase 2 — AWS

> Everything below this line costs money. Task 5 exists so that it costs a
> known, bounded amount.

### Task 5: Budget guardrail

- [ ] **Step 1: Check current spend**

AWS Console → Billing and Cost Management → Home. See what you are already being
charged; this project's ~$7.40/month lands on top of whatever is there.

- [ ] **Step 2: Create the budget**

Billing → Budgets → Create budget → **Cost budget**, monthly, fixed, **$10**.
Add alert thresholds at **50%**, **80%**, and **100% of actual**, each emailing
you.

*Why three thresholds:* the 100% alert tells you that you have already
overspent. The 50% alert, arriving around day 15 of a normal month, tells you
things are on track — and arriving on day 3 tells you something is wrong while
you can still act.

- [ ] **Step 3: Understand what this does and does not do**

It sends email. **AWS has no hard spend cap** — nothing stops the meter. If you
want automatic action, a Budget Action can stop the EC2 instance at a threshold,
but that also takes your site down, so it is a decision to make deliberately
rather than by default.

- [ ] **Step 4: Verify the alert address**

Confirm the notification email is one you actually read. An alert to a dead
address is worse than no alert, because you will believe you are covered.

---

### Task 6: Launch the instance

- [ ] **Step 1: Create a key pair**

EC2 Console → Network & Security → Key Pairs → Create. Name it
`emoji-waterfall`, type **ED25519**, format **.pem**. It downloads once.

```bash
mkdir -p ~/.ssh
mv ~/Downloads/emoji-waterfall.pem ~/.ssh/
```

*Why ED25519 over RSA:* shorter keys, faster, and the modern default. There is
no reason to pick RSA for a new key.

**AWS will never let you download this key again.** Lose it and the only
recovery is detaching the root volume and attaching it to another instance.

- [ ] **Step 2: Fix the key file permissions**

On Windows, `.pem` files inherit permissive ACLs and OpenSSH refuses to use
them, with an "UNPROTECTED PRIVATE KEY FILE" error. In PowerShell:

```powershell
icacls "$env:USERPROFILE\.ssh\emoji-waterfall.pem" /inheritance:r
icacls "$env:USERPROFILE\.ssh\emoji-waterfall.pem" /grant:r "$env:USERNAME:(R)"
```

*What that does:* `/inheritance:r` strips permissions inherited from parent
folders, then `/grant:r` gives read access to you alone. This is the Windows
equivalent of `chmod 400`.

- [ ] **Step 3: Find your public IP**

```bash
curl -s https://checkip.amazonaws.com
```

- [ ] **Step 4: Create the security group**

EC2 → Security Groups → Create. Name `emoji-waterfall-sg`. Inbound rules:

| Type | Port | Source |
|---|---|---|
| SSH | 22 | My IP (the address from Step 3) |
| HTTP | 80 | 0.0.0.0/0 |
| HTTPS | 443 | 0.0.0.0/0 |

Leave outbound at the default allow-all.

*Why 22 is restricted and 80/443 are not:* the web ports must be open or nobody
can visit the site. SSH open to the internet gets credential-scanned within
minutes of the instance booting. Note that a security group is **stateful** —
you do not add outbound rules for return traffic, unlike a network ACL.

If your ISP rotates your IP, SSH will start timing out. That is this rule
working, not a broken instance; edit the rule with your new address.

- [ ] **Step 5: Launch**

EC2 → Instances → Launch:

- **Name:** `emoji-waterfall`
- **AMI:** Amazon Linux 2023, **architecture arm64** — check this explicitly,
  the console defaults to x86_64
- **Instance type:** `t4g.nano`
- **Key pair:** `emoji-waterfall`
- **Network:** default VPC, existing security group `emoji-waterfall-sg`
- **Storage:** 8 GB **gp3**

*Why gp3 not gp2:* gp3 is cheaper per GB and includes 3,000 baseline IOPS
regardless of volume size. gp2 ties IOPS to size, so an 8 GB gp2 volume is
throttled to 100 IOPS.

- [ ] **Step 6: Allocate and associate an Elastic IP**

EC2 → Elastic IPs → Allocate → Associate with the instance.

*Why:* the auto-assigned public IP changes on every stop/start, which would
break your DNS A record. An Elastic IP is stable. Both cost the same $3.65/month
while attached — but an Elastic IP that is allocated and *not* attached to a
running instance still bills, so if you ever terminate the instance, release the
IP too.

- [ ] **Step 7: Connect**

```bash
ssh -i ~/.ssh/emoji-waterfall.pem ec2-user@<ELASTIC-IP>
```

Expected: a shell prompt. `ec2-user` is the default login for Amazon Linux.

If it hangs, the security group rule does not match your current IP. If it says
"permission denied (publickey)", the username or key path is wrong.

- [ ] **Step 8: Record the address**

Note the Elastic IP. It appears again in Tasks 8, 9, and 10.

---

### Task 7: Bootstrap the instance

All steps run over SSH on the instance.

- [ ] **Step 1: Update packages**

```bash
sudo dnf update -y
```

- [ ] **Step 2: Create the swapfile**

```bash
sudo fallocate -l 1G /swapfile
sudo chmod 600 /swapfile
sudo mkswap /swapfile
sudo swapon /swapfile
```

*Line by line:* `fallocate` reserves a 1 GB file without writing a gigabyte of
zeroes. `chmod 600` is required — swap holds process memory, so a world-readable
swapfile leaks anything in it, and `swapon` refuses insecure permissions.
`mkswap` writes the swap header. `swapon` activates it.

- [ ] **Step 3: Make swap survive reboots**

```bash
echo '/swapfile none swap sw 0 0' | sudo tee -a /etc/fstab
```

*Why:* `swapon` is not persistent. `/etc/fstab` is read at boot; without this
line your instance silently comes back with no swap, and you find out during the
next memory spike.

- [ ] **Step 4: Reduce swappiness**

```bash
echo 'vm.swappiness=10' | sudo tee /etc/sysctl.d/99-swappiness.conf
sudo sysctl -p /etc/sysctl.d/99-swappiness.conf
```

*Why:* the default of 60 makes the kernel swap fairly eagerly. Swap here is
EBS-backed and roughly a thousand times slower than RAM; you want it as a last
resort, not routine behaviour.

- [ ] **Step 5: Verify memory**

```bash
free -h
```

Expected: roughly 460 Mi total RAM and 1.0 Gi of swap, with swap `used` at 0.

- [ ] **Step 6: Install nginx**

```bash
sudo dnf install -y nginx
sudo systemctl enable --now nginx
```

`enable --now` both starts it and sets it to start at boot — two operations
people routinely forget to pair.

- [ ] **Step 7: Verify nginx from outside**

In a browser, visit `http://<ELASTIC-IP>`. Expected: the nginx welcome page.

**This is the single most valuable checkpoint in Phase 2.** It proves the
Elastic IP, the security group's port 80 rule, the VPC route table, and nginx
itself all work — before any of your code is involved. If this fails, no amount
of debugging your API will help.

- [ ] **Step 8: Create the application directories**

```bash
sudo mkdir -p /var/www/emoji-waterfall
sudo mkdir -p /opt/emoji-api
sudo chown -R ec2-user:ec2-user /var/www/emoji-waterfall /opt/emoji-api
```

*Why these two:* `/var/www` is the conventional home for static web content;
`/opt` is for self-contained application packages, which is exactly what a
published .NET app is. Setting ownership to `ec2-user` means the deploy script
can write there over SSH without `sudo`.

---

### Task 8: systemd service and first deploy

**Files:**
- Create: `deploy/emoji-api.service`
- Create: `deploy/deploy.sh`

- [ ] **Step 1: Write the systemd unit**

Create `deploy/emoji-api.service` locally. It must:

- Run `/opt/emoji-api/EmojiWaterfall.Api` as user `ec2-user`
- Set `WorkingDirectory` to `/opt/emoji-api`
- Set environment `ASPNETCORE_URLS=http://127.0.0.1:5000`
- Set environment `ASPNETCORE_ENVIRONMENT=Production`
- Restart on failure, with a restart delay
- Start as part of `multi-user.target`

*This is yours to write.* The critical line is `ASPNETCORE_URLS` — binding
`127.0.0.1` rather than `0.0.0.0` means Kestrel is reachable only from the
instance itself, so the only path in is through nginx. Even if someone opened
port 5000 in the security group by mistake, there would be nothing listening on
the public interface. The restart directive is what makes the service survive an
unhandled exception and a reboot.

- [ ] **Step 2: Write the deploy script**

Create `deploy/deploy.sh`. It must, in order:

1. `dotnet publish` the API — Release, `linux-arm64`, self-contained — to a
   local output directory
2. `npm run build` the frontend
3. `rsync` (or `scp -r`) the API output to `/opt/emoji-api/` on the instance
4. `rsync` the contents of `web/dist/` to `/var/www/emoji-waterfall/`
5. `ssh` in and `sudo systemctl restart emoji-api`

*This is yours to write.* Two things to get right: `set -euo pipefail` at the
top, so a failed build aborts instead of cheerfully deploying the previous
artifact; and take the instance address from a variable or argument rather than
hardcoding it, since it appears several times.

- [ ] **Step 3: Install the unit file on the instance**

```bash
scp -i ~/.ssh/emoji-waterfall.pem deploy/emoji-api.service ec2-user@<ELASTIC-IP>:/tmp/
ssh -i ~/.ssh/emoji-waterfall.pem ec2-user@<ELASTIC-IP> 'sudo mv /tmp/emoji-api.service /etc/systemd/system/ && sudo systemctl daemon-reload'
```

*Why `daemon-reload`:* systemd caches unit files in memory. Without it, your new
unit is invisible — and this is the most common systemd mistake there is. Re-run
it every time you change the unit file.

- [ ] **Step 4: Deploy**

```bash
chmod +x deploy/deploy.sh
./deploy/deploy.sh <ELASTIC-IP>
```

- [ ] **Step 5: Enable and start the service**

```bash
ssh -i ~/.ssh/emoji-waterfall.pem ec2-user@<ELASTIC-IP> 'sudo systemctl enable --now emoji-api && sudo systemctl status emoji-api --no-pager'
```

Expected: `active (running)`.

- [ ] **Step 6: Verify Kestrel on the instance**

```bash
ssh -i ~/.ssh/emoji-waterfall.pem ec2-user@<ELASTIC-IP> 'curl -i http://127.0.0.1:5000/api/health'
```

Expected: 200 and `{"status":"ok"}`. This is the second checkpoint: your API runs
on the server. nginx is not involved yet.

If it fails, `sudo journalctl -u emoji-api -n 50 --no-pager` shows the service's
own logs, which is where the real error will be. A common one is an architecture
mismatch — an x64 binary on ARM fails with a confusing exec format error.

- [ ] **Step 7: Commit**

```bash
git add deploy/
git commit -m "Add systemd unit and deploy script"
git push
```

---

### Task 9: nginx configuration — the slice goes live

**Files:**
- Create: `deploy/nginx.conf`

- [ ] **Step 1: Write the server block**

Create `deploy/nginx.conf` locally. It must:

- Listen on port 80
- Set `root /var/www/emoji-waterfall;` and `index index.html;`
- Serve `location /` with `try_files $uri $uri/ /index.html;`
- Proxy `location /api/` to `http://127.0.0.1:5000` with `proxy_set_header`
  lines for `Host`, `X-Real-IP`, `X-Forwarded-For`, and `X-Forwarded-Proto`

*This is yours to write.* Two subtleties worth understanding rather than
copying:

`try_files $uri $uri/ /index.html` is the single-page-app fallback. A React app
handles its own routing in the browser, so a direct request to a client-side
route is a path nginx cannot find on disk. This says "look for the file, then
the directory, and otherwise hand back `index.html`" so the app can route it.
Without it, any URL but `/` returns 404 as soon as you add routing.

The `proxy_set_header` lines matter because by default the proxied request
arrives at Kestrel claiming to come from `127.0.0.1`, carrying nginx's own Host
header. Anything that later cares about the caller's real address or the
original scheme — logging, rate limiting, redirect generation — gets it wrong.
`X-Forwarded-Proto` particularly matters once Task 10 adds TLS.

- [ ] **Step 2: Install it**

```bash
scp -i ~/.ssh/emoji-waterfall.pem deploy/nginx.conf ec2-user@<ELASTIC-IP>:/tmp/
ssh -i ~/.ssh/emoji-waterfall.pem ec2-user@<ELASTIC-IP> 'sudo mv /tmp/nginx.conf /etc/nginx/conf.d/emoji-waterfall.conf'
```

*Why `conf.d/`:* the stock `/etc/nginx/nginx.conf` has an `include
/etc/nginx/conf.d/*.conf` inside its `http` block. Dropping a file there adds
your server block without editing the distribution's config, which stays
upgradable. Your file therefore contains only a `server { }` block, not a whole
nginx config.

The default welcome-page server block also lives in the stock config and may
conflict on port 80. If it does, comment out its `server` block.

- [ ] **Step 3: Test the config before loading it**

```bash
ssh -i ~/.ssh/emoji-waterfall.pem ec2-user@<ELASTIC-IP> 'sudo nginx -t'
```

Expected: "syntax is ok" and "test is successful".

*Always run this before reloading.* `nginx -t` parses the config without
touching the running server. Reloading a broken config on a live server is how
you turn a typo into an outage.

- [ ] **Step 4: Reload**

```bash
ssh -i ~/.ssh/emoji-waterfall.pem ec2-user@<ELASTIC-IP> 'sudo systemctl reload nginx'
```

`reload` re-reads config while finishing in-flight requests; `restart` drops
them. Prefer reload.

- [ ] **Step 5: Verify from your own machine**

```bash
curl -i http://<ELASTIC-IP>/api/health
```

Expected: 200 and `{"status":"ok"}` — now travelling browser → nginx → Kestrel.

Then open `http://<ELASTIC-IP>` in a browser. Expected: your React page,
rendering `ok`.

**Phase 2 is complete.** The thin slice is live. Everything from here is domain,
TLS, and features.

- [ ] **Step 6: Commit**

```bash
git add deploy/
git commit -m "Add nginx configuration"
git push
```

---

## Phase 3 — Domain and TLS

### Task 10: DNS and HTTPS

- [ ] **Step 1: Register the domain**

Buy `emoji-waterfall.lol` at Namecheap.

- [ ] **Step 2: Add the DNS records**

Namecheap → Domain List → Manage → Advanced DNS. Delete any default parking
records, then add:

| Type | Host | Value | TTL |
|---|---|---|---|
| A | `@` | `<ELASTIC-IP>` | Automatic |
| A | `www` | `<ELASTIC-IP>` | Automatic |

`@` means the bare domain. Keep the TTL low (5 min) while you are still changing
things — it caps how long a wrong answer stays cached.

- [ ] **Step 3: Wait, then verify propagation**

```bash
nslookup emoji-waterfall.lol
```

Expected: your Elastic IP. This can take anywhere from a minute to an hour.

**Do not run certbot until this resolves.** Let's Encrypt validates by making an
HTTP request to your domain; if DNS has not propagated, validation fails and you
burn attempts against a rate limit.

- [ ] **Step 4: Install certbot**

```bash
ssh -i ~/.ssh/emoji-waterfall.pem ec2-user@<ELASTIC-IP>
sudo dnf install -y certbot python3-certbot-nginx
```

- [ ] **Step 5: Issue the certificate**

```bash
sudo certbot --nginx -d emoji-waterfall.lol -d www.emoji-waterfall.lol
```

Choose the redirect option when prompted, so HTTP traffic is sent to HTTPS.

*What just happened:* certbot proved you control the domain via the HTTP-01
challenge — it placed a file under `/.well-known/acme-challenge/`, and Let's
Encrypt fetched it over port 80 at your domain. Only someone controlling both
the DNS and the server can do that. Certbot then rewrote your nginx config in
place to add a 443 listener, the certificate paths, and the redirect.

Because certbot edits the config on the server,
`/etc/nginx/conf.d/emoji-waterfall.conf` now differs from your local
`deploy/nginx.conf`. Step 7 reconciles them.

- [ ] **Step 6: Verify the renewal timer**

```bash
sudo systemctl list-timers | grep certbot
sudo certbot renew --dry-run
```

Let's Encrypt certificates last 90 days. The timer renews automatically; the dry
run proves it will work rather than leaving you to find out on day 90.

- [ ] **Step 7: Pull the modified config back into the repo**

```bash
scp -i ~/.ssh/emoji-waterfall.pem ec2-user@<ELASTIC-IP>:/etc/nginx/conf.d/emoji-waterfall.conf deploy/nginx.conf
```

Review the diff so you can see exactly what certbot added — a good, short lesson
in what TLS termination looks like in practice. Then commit it, so your repo
matches the server and rebuilding from scratch stays possible.

- [ ] **Step 8: Final verification**

```bash
curl -i https://emoji-waterfall.lol/api/health
curl -i http://emoji-waterfall.lol/api/health
```

Expected: the first returns 200 with a valid certificate and no warnings; the
second returns a 301 redirect to HTTPS.

Open `https://emoji-waterfall.lol` in a browser. Expected: a padlock, and your
page rendering `ok`.

- [ ] **Step 9: Commit and tag**

```bash
git add deploy/
git commit -m "Add TLS configuration from certbot"
git tag phase-3-complete
git push --tags
```

*Why tag:* this is the last known-good state before emoji logic exists. If Phase
4 goes badly wrong, this is the point you compare against.

---

## Definition of done

- [ ] `https://emoji-waterfall.lol` loads over HTTPS with a valid certificate
- [ ] `https://emoji-waterfall.lol/api/health` returns `{"status":"ok"}`
- [ ] HTTP redirects to HTTPS
- [ ] `dotnet test` passes locally
- [ ] `sudo systemctl status emoji-api` reports `active (running)`
- [ ] The service survives `sudo reboot`
- [ ] AWS Budget is active at $10 with a verified email address
- [ ] `git status` is clean and everything is pushed

## What comes next

Plan 2 covers Phases 4–5: the emoji-api.com integration, SSM Parameter Store for
the access key, and the waterfall animation. Before it can be written, one
question needs answering — get an access key from emoji-api.com and run:

```bash
curl -s "https://emoji-api.com/emojis?access_key=YOUR_KEY" | head -c 2000
```

Look for whether emoji with skin tones carry a nested `variants` array or appear
as separate top-level entries. That determines the shape of the upstream model,
and it is logged as the open verification item in the spec.
