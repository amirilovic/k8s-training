#!/usr/bin/env bash
# Creates the broken scenarios for Lab 8, each in its own namespace (debug-1 ... debug-7).
set -euo pipefail
cd "$(dirname "$0")/broken"
kubectl apply -f 1.yaml -f 2.yaml -f 3.yaml -f 4.yaml -f 5.yaml -f 6.yaml -f 7.yaml >/dev/null
# Scenario 5 is a bad release: deploy v1, wait until it works, then "release" v3-broken on top.
kubectl -n debug-5 rollout status deployment/web --timeout=120s >/dev/null
kubectl -n debug-5 set image deployment/web web=ghcr.io/amirilovic/k8s-training-app:v3-broken >/dev/null
echo "Broken scenarios ready in namespaces debug-1 to debug-7. Start with: kubectl get pods -n debug-1"
