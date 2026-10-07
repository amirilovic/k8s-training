#!/usr/bin/env bash
# Checks that this machine is ready for the training. Takes 2 to 5 minutes.
# Creates a temporary cluster called "preflight", runs the training app in it,
# calls it through http://localhost:30080, and deletes the cluster again.
set -uo pipefail
cd "$(dirname "$0")"

problems=0
say()  { printf '%s\n' "$*"; }
ok()   { say "  OK    $*"; }
bad()  { say "  FAIL  $*"; problems=$((problems + 1)); }

say "== Environment"
case "$PWD" in
  /mnt/*) bad "the repository is on the Windows drive ($PWD). Clone it inside Ubuntu: cd ~ && git clone ..." ;;
  *)      ok "repository is inside Linux ($PWD)" ;;
esac
if grep -qi microsoft /proc/version 2>/dev/null; then
  ok "running in WSL ($(. /etc/os-release && echo "$PRETTY_NAME"))"
fi

say "== Tools"
for tool in docker kubectl kind helm; do
  if command -v "$tool" >/dev/null; then ok "$tool found"; else bad "$tool is not installed"; fi
done
command -v k9s >/dev/null && ok "k9s found" || say "  --    k9s not installed (optional)"
[ "$problems" -eq 0 ] || { say; say "Install the missing tools first."; exit 1; }

say "  docker  $(docker version --format '{{.Server.Version}}' 2>/dev/null || echo 'not reachable')"
say "  $(kubectl version --client 2>/dev/null | head -1)"
say "  $(kind version)"
say "  helm    $(helm version --short)"

if docker info >/dev/null 2>&1; then
  ok "Docker daemon reachable without sudo"
else
  bad "cannot talk to Docker. If the error says 'permission denied', run 'wsl --shutdown' in PowerShell and reopen Ubuntu"
  exit 1
fi

say "== Cluster"
kind delete cluster --name preflight >/dev/null 2>&1
if kind create cluster --name preflight --config kind.yaml --wait 120s >/tmp/preflight-kind.log 2>&1; then
  ok "kind cluster created with $(kubectl --context kind-preflight get nodes --no-headers | wc -l) nodes"
else
  bad "kind could not create the cluster, see /tmp/preflight-kind.log"
  tail -5 /tmp/preflight-kind.log
  exit 1
fi

K="kubectl --context kind-preflight"
$K create deployment web --image=ghcr.io/amirilovic/k8s-training-app:v1 --port=8080 >/dev/null
$K expose deployment web --type=NodePort --port=80 --target-port=8080 --overrides='{"spec":{"ports":[{"port":80,"targetPort":8080,"nodePort":30080}]}}' >/dev/null
if $K rollout status deployment/web --timeout=180s >/dev/null 2>&1; then
  ok "training app image pulled and running"
else
  bad "the training app did not start"; $K get pods
fi

answer=""
for _ in $(seq 1 15); do
  answer=$(curl -s --max-time 3 http://localhost:30080/) && [ -n "$answer" ] && break
  sleep 2
done
if [ -n "$answer" ]; then ok "app answers on http://localhost:30080: $answer"; else bad "nothing answers on http://localhost:30080"; fi

kind delete cluster --name preflight >/dev/null 2>&1 && ok "temporary cluster deleted"

say
if [ "$problems" -eq 0 ]; then
  say "== All good. You are ready for Monday."
else
  say "== $problems problem(s). Send this whole output to the trainer."
fi
[ "$problems" -eq 0 ]
