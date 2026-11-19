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

### SSH key with a passphrase (Windows)

Git Bash ships its own `ssh`, which cannot reach the Windows `ssh-agent`. Then
`docker --context emoji-droplet` asks for the passphrase or fails. Fix this once:

1. Open PowerShell **as administrator** and start the agent service:
   `Get-Service ssh-agent | Set-Service -StartupType Automatic; Start-Service ssh-agent`
2. In a normal PowerShell window, run `ssh-add` and enter the passphrase.
3. Add this line to `~/.bashrc`, so Git Bash uses the Windows OpenSSH client,
   which can reach the agent:

   ```bash
   export PATH="/c/Windows/System32/OpenSSH:$PATH"
   ```

Open a new Git Bash window after you change `~/.bashrc`.

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

## DNS at Namecheap

Point the domain at the droplet. Caddy needs this before it can get an
HTTPS certificate.

1. In Namecheap, open **Domain List > Manage** for `emojiwaterfall.com`, then
   **Advanced DNS**.
2. Delete the parking records: the `www` CNAME that points to the parking
   page, and any URL redirect record.
3. Add two records. Use your droplet address for `<IP>`.

   | Type | Host | Value | TTL |
   |---|---|---|---|
   | A Record | `@` | `<IP>` | Automatic |
   | A Record | `www` | `<IP>` | Automatic |

4. Check that DNS answers with the droplet address. Use the resolver
   `1.1.1.1`, not your local one, which may cache old answers:

   ```bash
   nslookup emojiwaterfall.com 1.1.1.1
   nslookup www.emojiwaterfall.com 1.1.1.1
   ```

   Both must show `<IP>`. If not, wait a few minutes and try again.

**Do not deploy until both names show `<IP>`.** Caddy asks Let's Encrypt for
a certificate when it starts with a domain. If DNS is wrong, the check fails.
Let's Encrypt limits failed requests, so do not redeploy in a loop.

5. Deploy with the real domain:

   ```bash
   DOCKER_CONTEXT=emoji-droplet bash deploy/deploy.sh 0.1.0 emojiwaterfall.com
   ```

6. Check HTTPS. Caddy may need a short time to get the certificate.

   | Command | Expected result |
   |---|---|
   | `curl -sI https://emojiwaterfall.com` | `HTTP/2 200` |
   | `curl -sI https://www.emojiwaterfall.com` | `301`, with `location: https://emojiwaterfall.com/` |
   | `curl -sI http://emojiwaterfall.com` | a redirect to `https://` |

## Set up automatic deploys

When you push a tag that starts with `v`, GitHub Actions runs `test`, then
`publish`, then `deploy`. The `deploy` job runs `deploy/deploy.sh` against the
droplet. If the new tasks fail, Swarm rolls back by itself, the script exits
with an error, and the job fails. Do these steps once.

GitHub needs its own way in. Use a separate deploy-only key, not your own
key. If it leaks, you delete one line on the droplet and nothing else is
affected. The key logs in as the `deploy` user. That user is in the `docker`
group but has no `sudo`, so it can run `docker` commands and cannot change
the server itself.

1. Create the key pair on your laptop. `-N ""` means no passphrase, because
   a workflow cannot type one.

   ```bash
   ssh-keygen -t ed25519 -N "" -f emoji-deploy -C github-deploy
   ```

   This makes `emoji-deploy` (private) and `emoji-deploy.pub` (public). Never
   commit them.

2. Add the public key to the `deploy` user on the droplet. The user `ops`
   does not own `/home/deploy/.ssh/authorized_keys`, so `ops` writes to it
   with `sudo tee -a`. The `-a` adds a line and keeps the old ones.

   ```bash
   ssh ops@<IP> 'sudo tee -a /home/deploy/.ssh/authorized_keys' < emoji-deploy.pub
   ```

   The file must stay owned by `deploy` with mode 600, or `sshd` ignores it.
   Check it:

   ```bash
   ssh ops@<IP> 'sudo ls -l /home/deploy/.ssh/authorized_keys'
   ```

   Expected: `-rw------- 1 deploy deploy`. If the owner or mode is wrong, run
   `sudo chown deploy:deploy` and `sudo chmod 600` on the file.

3. Capture the droplet's host key. The workflow will trust only this key, so
   it never connects to an unknown host. Save the output in a file:

   ```bash
   ssh-keyscan -t ed25519 <IP> > known_hosts.txt
   ssh-keygen -lf known_hosts.txt
   ```

4. Compare the fingerprint with the one on the droplet. Ask the droplet
   itself, over your trusted `ops` login:

   ```bash
   ssh ops@<IP> ssh-keygen -lf /etc/ssh/ssh_host_ed25519_key.pub
   ```

   The two `SHA256:...` values must be identical. If they differ, stop. Do
   not continue until you know why.

5. Add three repository secrets in GitHub, under **Settings > Secrets and
   variables > Actions > New repository secret**. Or use `gh secret set NAME < file`.

   | Secret | Value |
   |---|---|
   | `DEPLOY_HOST` | The droplet IP address, `<IP>` |
   | `DEPLOY_SSH_KEY` | The whole private key file `emoji-deploy`, including the `BEGIN` and `END` lines |
   | `DEPLOY_KNOWN_HOSTS` | The one line from `known_hosts.txt` |

   With the CLI:

   ```bash
   gh secret set DEPLOY_SSH_KEY < emoji-deploy
   gh secret set DEPLOY_KNOWN_HOSTS < known_hosts.txt
   printf '%s' '<IP>' | gh secret set DEPLOY_HOST
   ```

6. Delete the local key files. GitHub now holds the private key, and you do
   not need another copy.

   ```bash
   rm emoji-deploy emoji-deploy.pub known_hosts.txt
   ```

## Roll back by hand

Swarm rolls back by itself when a new version fails to start. To go back to
an older version that started fine, deploy its tag again from your laptop:

```bash
DOCKER_CONTEXT=emoji-droplet bash deploy/deploy.sh <previous tag> emojiwaterfall.com
```

`<previous tag>` is an image version without the `v`, for example `0.1.0`.
Check which version runs now with:

```bash
docker --context emoji-droplet service ls --format '{{.Name}} {{.Image}}'
```

## Check health and logs

Run these from your laptop. Each one asks the droplet over the Docker context.

```bash
docker --context emoji-droplet service ls
docker --context emoji-droplet service ps emoji_backend
docker --context emoji-droplet service logs -f emoji_backend
docker --context emoji-droplet stats --no-stream
```

- `service ls` shows each service and its replicas. A healthy stack shows
  `emoji_caddy` 1/1, `emoji_frontend` 2/2, and `emoji_backend` 2/2.
- `service ps <service>` shows the tasks of one service. Healthy tasks have the
  state `Running`. A task with `Rejected` or `Failed` did not start or crashed.
  Read its error text in the `ERROR` column. Add `--no-trunc` to see all of it.
- `service logs -f emoji_backend` follows the backend log. Press Ctrl+C to stop.
  Use it to see why a task failed.
- `stats --no-stream` prints memory and CPU once for each container on the
  droplet. Compare memory use with the limit in the same row. The droplet has
  only 1 GB, so memory is the first thing to run out.

## Load test

The load test uses [k6](https://k6.io), run in a Docker container, so you
install nothing. It sends 20 virtual users to `GET /api/emojis?count=50` for
60 seconds. The API serves from a cache, so the test does not call
emoji-api.com on each request.

```bash
docker run --rm -i -e BASE_URL=https://emojiwaterfall.com grafana/k6:2.4.0 run - < deploy/load-test.js
```

The script is sent on standard input (`run -` and `<`). This avoids path
problems in Git Bash on Windows.

While the test runs, open a second Git Bash window and watch memory:

```bash
docker --context emoji-droplet stats --no-stream
ssh ops@<IP> free -m
```

At the end, k6 marks each threshold with a check mark (pass) or a cross (fail).
Read these lines:

- `http_req_failed`: the share of failed requests. It must stay under 1%.
- `http_req_duration`: look at `p(95)`. 95% of requests were faster than this
  value. It must stay under 500 ms.
- `checks`: the status is 200 and the body is a list of 50 emojis.

The test passes when:

- both thresholds pass,
- every container stays under its memory limit, and
- `free -m` shows more than 100 MB in the `available` column.

If it does not pass, write down the numbers. Then resize the droplet to
2 GB in DigitalOcean and run the test again.
