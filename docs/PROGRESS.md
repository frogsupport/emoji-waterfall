# Where we are

Last updated: 2026-11-04

## Working agreement

**The developer writes all production code and runs the deploys.** Claude
inspects, explains, and links to documentation, and does not edit `api/`, `web/`,
or deployment files, or rebuild containers, unless asked. Docs in `docs/` are
Claude's to write. Explanation is a deliverable, not a bonus: the developer is
here to learn how every moving part works.

## The plan changed

The app now deploys to a **DigitalOcean droplet with Docker Swarm**, not to AWS.
`README.md` holds the current requirements.

The AWS spec and plan below are **out of date for infrastructure, deployment, and
domain**. Ignore their EC2, systemd, SSM, certbot, and `emoji-waterfall.lol`
sections. Their app design notes (such as the verified emoji-api.com response
shape) are still useful.

- `docs/superpowers/specs/2026-08-29-emoji-waterfall-design.md`
- `docs/superpowers/plans/2026-08-29-thin-slice-to-production.md`

## Decisions

| Area | Decision |
|---|---|
| Host | DigitalOcean droplet, single node; size to be set by measurement |
| Orchestration | Docker Swarm |
| Replicas | 2 frontend, 2 backend |
| Crash recovery | Swarm `deploy.restart_policy` |
| Registry | GitHub Container Registry (`ghcr.io/frogsupport/`), public images |
| Images | Built and pushed by a GitHub Actions workflow on version tags, pulled by the droplet |
| Routing | nginx in the frontend image proxies `/api/` to `backend:8080` |
| HTTPS | Caddy as a single edge proxy, with a volume for `/data` |
| Domain | emojiwaterfall.com |

Swarm gives `backend` one stable virtual IP, so the nginx config does not need a
`resolver` workaround for restarted containers. Two replicas on one droplet
protect against a crashed container, not against losing the droplet.

## Done

- ASP.NET API with `GET /api/health` and `GET /api/emojis?count=N`, using
  HybridCache and priming the cache at startup.
- React app fetching and displaying emojis.
- Dockerfiles for `api/` and `web/`, `web/nginx.conf`, and a local `compose.yml`
  using `api.env` (gitignored). The proxy target is fixed in `nginx.conf`.
- emoji-api.com access key rotated.

## Next action

**Step 1: commit the Docker work.** The Dockerfiles, `.dockerignore` files,
`compose.yml`, `web/nginx.conf`, and `README.md` are untracked, and the
`.gitignore` and `Program.cs` changes are uncommitted.

## Deployment steps

Open to change.

1. Commit the Docker work.
2. Add the GitHub Actions workflow, push a version tag, and confirm both images
   are public on GHCR.
3. Write a Swarm stack file and get 2 + 2 replicas running locally. Kill a
   container and watch Swarm replace it.
4. Measure memory and CPU with resource limits on, under load, then pick a
   droplet size.
5. Set up the droplet: Docker, Swarm, a DigitalOcean Cloud Firewall (22, 80, 443
   only), log rotation, and the API key. Deploy and test by IP address.
6. Add DNS for emojiwaterfall.com, then Caddy for HTTPS.
7. Write down the repeatable deploy steps, then rerun the crash and load tests in
   production.

## Open questions

- How should the API key reach the container: a Docker secret (needs ASP.NET's
  key-per-file provider) or an environment variable?
- Does a droplet already exist, and is it running anything?
- Which emoji variants should `/api/emojis` return? Not a deployment blocker.

## Known issues

None of these block deployment.

- `/api/health` returns OK even when the API key is missing, so a broken deploy
  looks healthy. Worth deciding before adding a health check.
- `/api/emojis` has no tests; only the health endpoint does.
- In `web/src/App.tsx`, the `try/catch` does not await `loadData()`, so a failed
  fetch is not caught and the page likely stays in its loading state.
