#!/usr/bin/env bash
# Checks the capstone deployment.
# Usage: capstone/check.sh <namespace> <ingress-host>
#   e.g. capstone/check.sh capstone capstone-anna-ben.localhost
set -uo pipefail

NS="${1:-capstone}"
HOST="${2:-}"
APP_IMAGE="k8s-training-app"
pass=0
fail=0

ok()   { echo "  PASS  $1"; pass=$((pass + 1)); }
bad()  { echo "  FAIL  $1"; [ -n "${2:-}" ] && echo "        hint: $2"; fail=$((fail + 1)); }
check() { if [ "$1" = "true" ]; then ok "$2"; else bad "$2" "${3:-}"; fi; }

echo "Checking namespace '$NS'"

if ! kubectl get namespace "$NS" >/dev/null 2>&1; then
  bad "namespace $NS exists" "kubectl create namespace $NS"
  exit 1
fi

# Find the app Deployment by its image.
DEPLOY=$(kubectl -n "$NS" get deploy -o jsonpath='{range .items[*]}{.metadata.name}{" "}{.spec.template.spec.containers[0].image}{"\n"}{end}' \
  | awk -v img="$APP_IMAGE" 'index($2, img) {print $1; exit}')

if [ -z "$DEPLOY" ]; then
  bad "a Deployment runs the training app image" "no Deployment in $NS uses an image containing $APP_IMAGE"
  echo; echo "$pass passed, $fail failed"; exit 1
fi
ok "Deployment '$DEPLOY' runs the training app"

jp() { kubectl -n "$NS" get deploy "$DEPLOY" -o jsonpath="$1" 2>/dev/null; }
c='{.spec.template.spec.containers[0]'

check "$([ -n "$(helm -n "$NS" list -q 2>/dev/null)" ] && echo true)" "installed with Helm" "helm install <release> <chart> -n $NS"

ready=$(jp '{.status.readyReplicas}'); ready=${ready:-0}
check "$([ "$ready" -ge 2 ] && echo true)" "at least 2 ready replicas (now $ready)"

check "$([ -n "$(jp "$c.readinessProbe}")" ] && echo true)" "readiness probe configured" "readinessProbe on /readyz"
check "$([ -n "$(jp "$c.livenessProbe}")" ] && echo true)" "liveness probe configured" "livenessProbe on /healthz"
check "$([ -n "$(jp "$c.resources.requests.cpu}")" ] && [ -n "$(jp "$c.resources.requests.memory}")" ] && echo true)" \
  "CPU and memory requests set" "resources.requests.cpu and resources.requests.memory"
check "$([ -n "$(jp "$c.resources.limits.memory}")" ] && echo true)" "memory limit set" "resources.limits.memory"

check "$([ -n "$(jp '{.spec.template.spec.volumes[*].configMap.name}')$(jp "$c.envFrom[*].configMapRef.name}")" ] && echo true)" \
  "configuration comes from a ConfigMap" "mount a ConfigMap at /config"
check "$([ -n "$(jp '{.spec.template.spec.volumes[*].secret.secretName}')$(jp "$c.env[*].valueFrom.secretKeyRef.name}")$(jp "$c.envFrom[*].secretRef.name}")" ] && echo true)" \
  "secrets come from a Secret" "mount a Secret at /secrets or use secretKeyRef"

hpa=$(kubectl -n "$NS" get hpa -o jsonpath="{range .items[?(@.spec.scaleTargetRef.name=='$DEPLOY')]}{.metadata.name}{end}" 2>/dev/null)
check "$([ -n "$hpa" ] && echo true)" "HorizontalPodAutoscaler targets $DEPLOY" "autoscaling/v2 HPA with scaleTargetRef.name: $DEPLOY"

pg=$(kubectl -n "$NS" get pods -o jsonpath='{range .items[*]}{.spec.containers[0].image}{" "}{.status.containerStatuses[0].ready}{"\n"}{end}' 2>/dev/null | awk '$1 ~ /postgres/ && $2 == "true"' | head -1)
check "$([ -n "$pg" ] && echo true)" "Postgres pod is ready"
pvc=$(kubectl -n "$NS" get pvc -o jsonpath='{range .items[*]}{.status.phase}{"\n"}{end}' 2>/dev/null | grep -c Bound)
check "$([ "$pvc" -ge 1 ] && echo true)" "Postgres data on a bound PersistentVolumeClaim"

hosts=$(kubectl -n "$NS" get ingress -o jsonpath='{range .items[*]}{.spec.rules[*].host}{" "}{end}' 2>/dev/null)
if [ -z "$HOST" ]; then HOST=$(echo "$hosts" | awk '{print $1}'); fi
check "$([ -n "$hosts" ] && echo true)" "Ingress exists (hosts: ${hosts:-none})" "an Ingress with ingressClassName: traefik"

if [ -n "$HOST" ]; then
  url="http://$HOST:30081/"
  first=$(curl -s --max-time 5 --resolve "$HOST:30081:127.0.0.1" "$url")
  second=$(curl -s --max-time 5 --resolve "$HOST:30081:127.0.0.1" "$url")
  check "$(echo "$first" | grep -q 'version:' && echo true)" "app answers at $url" "is the Ingress host right, and is Traefik on NodePort 30081?"
  v1=$(echo "$first" | sed -n 's/.*visits: \([0-9]*\).*/\1/p')
  v2=$(echo "$second" | sed -n 's/.*visits: \([0-9]*\).*/\1/p')
  check "$([ -n "$v1" ] && [ -n "$v2" ] && [ "$v2" -gt "$v1" ] && echo true)" "visits are stored in Postgres (got: ${v1:-?} then ${v2:-?})" \
    "$(echo "$first" | grep -o 'visits: unavailable.*' || echo 'is ConnectionStrings__Default set, and has the migration Job run?')"
fi

echo
echo "$pass passed, $fail failed"
[ "$fail" -eq 0 ]
