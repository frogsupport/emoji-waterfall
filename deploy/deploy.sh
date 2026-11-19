#!/usr/bin/env bash
# Deploy the emoji stack to the active Docker endpoint (DOCKER_CONTEXT or
# DOCKER_HOST selects it). Exits non-zero if Swarm rolls back any service.
#
# Usage: deploy.sh <tag> <domain>
#   tag:    image tag without the "v", for example 0.1.0
#   domain: emojiwaterfall.com in production, localhost on a laptop,
#           or :80 to serve plain HTTP by IP address
set -euo pipefail

if [ $# -ne 2 ]; then
  echo "usage: deploy.sh <tag> <domain>" >&2
  exit 2
fi

script_dir=$(cd "$(dirname "$0")" && pwd)

export TAG=$1
export DOMAIN=$2

# Redirect www only for a real domain name.
if [[ $DOMAIN == *.* && $DOMAIN != :* ]]; then
  export WWW_DOMAIN="www.$DOMAIN"
else
  export WWW_DOMAIN="www.localhost"
fi

CADDYFILE_HASH=$(sha256sum "$script_dir/Caddyfile" | cut -c1-12)
export CADDYFILE_HASH

# A rollback state older than this deploy belongs to an earlier deploy.
# The 5 seconds allow for a clock difference with the server.
start=$(($(date +%s) - 5))

docker stack deploy --detach=false -c "$script_dir/stack.yml" emoji

failed=0
for service in $(docker stack services emoji --format '{{.Name}}'); do
  status=$(docker service inspect "$service" \
    --format '{{if .UpdateStatus}}{{if .UpdateStatus.StartedAt}}{{.UpdateStatus.State}} {{.UpdateStatus.StartedAt.Unix}}{{end}}{{end}}')
  [ -z "$status" ] && continue

  state=${status% *}
  started=${status#* }
  if ((started >= start)) && [[ $state == rollback* || $state == paused ]]; then
    echo "deploy failed: $service is $state" >&2
    failed=1
  fi
done

exit $failed
