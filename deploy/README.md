# Deploying Emoji Waterfall

The site runs on one DigitalOcean droplet with Docker Swarm.

```
browser -> Caddy (HTTPS) -> frontend (nginx, 2 replicas) -> /api/ -> backend (ASP.NET, 2 replicas)
```

| File | What it does |
|---|---|
| `stack.yml` | The Swarm stack: services, replicas, the secret, update and rollback rules |
| `Caddyfile` | HTTPS for the domain, and the `www` redirect |
| `deploy.sh` | The only deploy command, used from your laptop and from GitHub Actions |
| `cloud-init.yml` | Sets up a new droplet: users, SSH, Docker, Swarm, log rotation, swap |

Run the commands below in **Git Bash** unless the step says otherwise.

## Run the stack on your laptop

Docker Desktop must be running.

```bash
docker swarm init                     # once; undo with: docker swarm leave --force
docker build -t ghcr.io/frogsupport/emoji-api:local api
docker build -t ghcr.io/frogsupport/emoji-web:local web
printf '%s' "$(sed -n 's/^EmojiApi__AccessKey=//p' api.env | tr -d '\r')" \
  | docker secret create emoji_api_access_key -
bash deploy/deploy.sh local localhost
```

Open https://localhost and accept the local certificate. Remove the stack
with `docker stack rm emoji`.

## Create the droplet

1. Make sure your laptop has an SSH key: `ls ~/.ssh/id_ed25519.pub`. If not,
   run `ssh-keygen -t ed25519`.
2. In DigitalOcean, open **Settings > Security > SSH Keys** and add the
   contents of `~/.ssh/id_ed25519.pub`.
3. Create a droplet:
   - Image: **Ubuntu 24.04 (LTS) x64**
   - Size: **Basic, Regular, 1 GB / 1 CPU** ($6/month)
   - Region: the one closest to you
   - Authentication: **SSH Key**, select the key from step 2
   - **Advanced options > Add initialization scripts**: paste all of
     `deploy/cloud-init.yml`
4. Create a firewall in **Networking > Firewalls**. Inbound rules:

   | Type | Protocol | Port |
   |---|---|---|
   | SSH | TCP | 22 |
   | HTTP | TCP | 80 |
   | HTTPS | TCP | 443 |
   | Custom | UDP | 443 |

   Keep the default outbound rules. Apply the firewall to the droplet.
5. Write down the droplet's public IP address. Below, `<IP>` means that address.

## Connect from your laptop

```bash
ssh ops@<IP> cloud-init status --wait   # waits until setup finishes: "status: done"
docker context create emoji-droplet --docker "host=ssh://ops@<IP>"
docker --context emoji-droplet info --format '{{.Swarm.LocalNodeState}}'   # "active"
```

The first `ssh` asks you to trust the droplet's host key. Answer `yes`.

A Docker context sends `docker` commands to another machine over SSH. Add
`--context emoji-droplet` to any command, or set `DOCKER_CONTEXT=emoji-droplet`.

## Create the API key secret

Do this once per droplet. Use `printf`, not `echo`, so no newline is added.
The API trims whitespace anyway, but the key should be exact.

```bash
printf '%s' '<your emoji-api.com key>' \
  | docker --context emoji-droplet secret create emoji_api_access_key -
```

Swarm secrets cannot change. To rotate the key:

1. Create a new secret, for example `emoji_api_access_key_v2`.
2. In `stack.yml`, change the name under the top-level `secrets:` to
   `name: emoji_api_access_key_v2` and keep the key `emoji_api_access_key`.
3. Deploy. After it succeeds, remove the old secret with
   `docker --context emoji-droplet secret rm emoji_api_access_key`.

## Deploy by hand

```bash
DOCKER_CONTEXT=emoji-droplet bash deploy/deploy.sh <tag> <domain>
```

- `<tag>`: an image version without the `v`, for example `0.1.0`.
- `<domain>`: `emojiwaterfall.com` in production, or `:80` to serve plain
  HTTP by IP address before DNS is ready.

The command waits until Swarm finishes. It exits with an error if Swarm
rolled back any service.
