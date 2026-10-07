# Kubernetes training

Three days of hands-on Kubernetes for developers, on a local kind cluster in WSL2. Monday 12 to Wednesday 14 October 2026.

Before the training, complete [SETUP.md](SETUP.md) and run `setup/preflight.sh`.

## Monday morning

```bash
cd ~
git clone https://github.com/amirilovic/k8s-training.git
cd k8s-training
setup/create-cluster.sh
mkdir -p work
```

Write your own YAML files in `work/`. Git ignores that folder.

## Labs

| Day | Lab | Topic |
|---|---|---|
| Monday | [Lab 1](labs/01-pods/README.md) | kubectl, Pods, namespaces |
| Monday | [Lab 2](labs/02-deployments/README.md) | Deployments, scaling, rolling updates, rollback |
| Tuesday | [Lab 3](labs/03-services/README.md) | Services and cluster DNS |
| Tuesday | [Lab 4](labs/04-config/README.md) | ConfigMaps and Secrets |
| Tuesday | [Lab 5](labs/05-probes-limits/README.md) | Probes, requests and limits |
| Wednesday | [Lab 6](labs/06-database/README.md) | Postgres, PersistentVolumeClaim, migration Job |
| Wednesday | [Lab 7](labs/07-debugging/README.md) | Debugging broken deployments |
| Wednesday | [Lab 8](labs/08-hpa/README.md) | Horizontal Pod Autoscaler |
| Wednesday | [Lab 9](labs/09-helm/README.md) | Helm |
| Wednesday | [Lab 10](labs/10-ingress/README.md) | Ingress |
| Wednesday | [Capstone](capstone/README.md) | Everything together, in pairs |

## If you fall behind

Every lab has a solution in `solutions/`. Apply the previous lab's solution and continue from there, for example before Lab 4:

```bash
kubectl apply -f solutions/03-services/
```

## Repository layout

| Path | Contents |
|---|---|
| `app/` | The .NET training app and its Dockerfile, see [app/README.md](app/README.md) |
| `setup/` | kind cluster config, preflight check, metrics-server and Traefik settings |
| `labs/` | One folder per lab with instructions |
| `solutions/` | The end state of each lab |
| `capstone/` | Capstone task and `check.sh` |

## Ports on your machine

| Address | What answers |
|---|---|
| `http://localhost:8080` | Whatever you `kubectl port-forward` |
| `http://localhost:30080` | Your NodePort Service from Lab 3 |
| `http://<name>.localhost:30081` | Traefik, the ingress controller (Labs 9 and 10, capstone) |
