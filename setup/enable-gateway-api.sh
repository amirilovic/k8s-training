#!/usr/bin/env bash
# Lab 10, step 6: installs the Gateway API resource definitions and turns on Traefik's
# Gateway API support. Traefik then creates a Gateway called traefik-gateway in the
# traefik namespace that accepts HTTPRoutes from every namespace.
set -euo pipefail
cd "$(dirname "$0")"
kubectl apply --server-side -f https://github.com/kubernetes-sigs/gateway-api/releases/download/v1.6.3/standard-install.yaml
helm upgrade traefik traefik/traefik --version 41.6.1 -n traefik -f traefik-values.yaml \
  --set providers.kubernetesGateway.enabled=true \
  --set gateway.listeners.web.namespacePolicy.from=All \
  --wait
kubectl get gatewayclass,gateway -n traefik
