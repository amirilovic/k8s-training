#!/usr/bin/env bash
# Monday morning: creates the training cluster (1 control plane, 2 workers).
set -euo pipefail
cd "$(dirname "$0")"
if kind get clusters 2>/dev/null | grep -qx training; then
  echo "A cluster called 'training' already exists. Delete it with: kind delete cluster --name training"
  exit 1
fi
kind create cluster --config kind.yaml --wait 120s
kubectl get nodes
