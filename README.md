# Kubernetes training

Three days of hands-on Kubernetes for developers, on a local kind cluster in WSL2. Monday 12 to Wednesday 14 October 2026.

Before the training, complete [SETUP.md](SETUP.md) and run `setup/preflight.sh`. The schedule is in [AGENDA.md](AGENDA.md).

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
| Tuesday | [Lab 4](labs/04-ingress/README.md) | Ingress |
| Tuesday | [Lab 5](labs/05-config/README.md) | ConfigMaps and Secrets |
| Tuesday | [Lab 6](labs/06-probes/README.md) | Readiness and liveness probes |
| Wednesday | [Lab 7](labs/07-limits-hpa/README.md) | Requests, limits, Horizontal Pod Autoscaler |
| Wednesday | [Lab 8](labs/08-jobs/README.md) | Jobs and CronJobs |
| Wednesday | [Lab 9](labs/09-database/README.md) | Postgres, PersistentVolumeClaim, migration Job |
| Wednesday | [Lab 10](labs/10-debugging/README.md) | Debugging broken deployments |

## If you fall behind

Every lab has a solution in `solutions/`. Apply the previous lab's solution and continue from there, for example before Lab 4:

```bash
kubectl apply -f solutions/03-services/
```

## Repository layout

| Path | Contents |
|---|---|
| `app/` | The .NET training app and its Dockerfile, see [app/README.md](app/README.md) |
| `setup/` | kind cluster config, preflight check, metrics-server and Traefik manifests |
| `labs/` | One folder per lab with instructions |
| `solutions/` | The end state of each lab |
| `AGENDA.md`, `TRAINER.md` | Participant schedule; the trainer's full plan with timings and prep notes |
| `slides/` | One HTML deck per day, open `slides/index.html`. Keys: arrows, N notes, O overview, T theme, F fullscreen |

## Ports on your machine

| Address | What answers |
|---|---|
| `http://localhost:8080` | Whatever you `kubectl port-forward` |
| `http://localhost:30080` | Your NodePort Service from Lab 3 |
| `http://<name>.localhost:30081` | Traefik, the ingress controller (Lab 4 onward) |
