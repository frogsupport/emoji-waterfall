# Where we are

Last updated: 2026-08-29

## Read these first

- Spec: `docs/superpowers/specs/2026-08-29-emoji-waterfall-design.md`
- Plan (Phases 1–3): `docs/superpowers/plans/2026-08-29-thin-slice-to-production.md`

## Working agreement

**The developer writes all production code.** Claude supplies commands,
contracts, and explanations, and asks before writing any file in `api/`,
`web/`, or `deploy/`. Docs in `docs/` are Claude's to write. Walk through what
each step is doing and why — explanation is a deliverable, not a bonus.

## Done

- **Phase 0 complete.** Repo initialized, pushed to
  `github.com/frogsupport/emoji-waterfall` (public, default branch `master`).
  Nested `.gitignore` strategy: root file for OS/editor/secrets, `api/.gitignore`
  from `dotnet new gitignore`, `web/.gitignore` to come from the Vite scaffold.
- Spec written and approved. Decisions locked: single EC2 `t4g.nano` (ARM64,
  Amazon Linux 2023, us-east-1) running nginx + Kestrel; nginx serves the React
  build and reverse-proxies `/api`; CSS keyframe animation for v1; emoji-api.com
  as the upstream; DNS at Namecheap, no Route53; ~$7.40/month accepted with an
  AWS Budget alert at $10.
- Plan written for Phases 1–3, ending with the thin slice live on HTTPS.
- **Upstream API verified against the live endpoint.** Findings are in the
  spec under "Upstream shape — VERIFIED": `variants` is nested and carries only
  `slug` + `character`; every `unicodeName`/`slug` is version-prefixed
  (`E0.6 waving hand`) and needs stripping; there is no `component` group to
  filter despite what the docs imply.

## Next action

**Task 1 of the plan** — API scaffold and health endpoint. Nothing in `api/`
has been created yet beyond `.gitignore`. Start with:

```bash
cd api
dotnet new sln -n EmojiWaterfall
dotnet new web -n EmojiWaterfall.Api
dotnet sln add EmojiWaterfall.Api/EmojiWaterfall.Api.csproj
```

Then the developer writes: two csproj properties
(`ServerGarbageCollection` false, `InvariantGlobalization` true) and
`GET /api/health` returning `{"status":"ok"}`.

## Open question — blocks Phase 4, not Phase 1

Which variants belong in the flattened `/api/emojis`. Was asked, not yet
answered:

| Option | Entries | Note |
|---|---|---|
| Skin tones only (recommended) | ~2,600 | detect by slug suffix; drops hair-colour and gendered permutations |
| All variants | 3,655 | 👩 alone contributes 266 near-identical entries |
| Base emoji only | 1,859 | reverses the earlier "add all skin tone variants" call |

Phases 1–3 do not depend on this. Plan 2 (Phases 4–5) cannot be written until
it is answered.

## Housekeeping

- The emoji-api.com access key was pasted into a chat transcript. Free tier, low
  stakes, but worth rotating. It belongs in `dotnet user-secrets` locally and
  SSM Parameter Store on EC2 — never in the repo.
- Nothing billable has been created in AWS yet. Phase 2 Task 5 (the budget)
  comes before any resource that costs money.
