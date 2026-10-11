# Kubernetes training, 2026-10-12 to 2026-10-14

Audience: 8 experienced developers with no hands-on Kubernetes. Laptops run Windows, participants have admin rights, WSL2 is allowed, and there is no TLS-intercepting proxy.
Format: Monday 4h, Tuesday 4h, Wednesday 6h. Target split is about 25% talking, 75% hands-on.

## Lab environment

Every participant runs the same stack, and every command in the exercises is a bash command run inside WSL2. Nobody types `kubectl` into PowerShell. This removes the whole class of PowerShell quoting, `curl` alias, `$VAR` and CRLF problems, and lets one set of exercise instructions work for everyone (including you on macOS).

| Piece | Choice | Why |
|---|---|---|
| Linux layer | WSL2 + Ubuntu 26.04 | Same shell as the exercises and as production CI |
| Container runtime | Docker Engine installed inside the Ubuntu distro (apt, from download.docker.com), not Docker Desktop | Engine is Apache 2.0 licensed, so the company's Docker Desktop licence status does not matter. Labs only use the `docker` CLI. Needs systemd in WSL, which `wsl --install` enables on current Ubuntu images |
| Cluster | kind, 1 control plane + 2 workers | Multi-node is needed for scheduling, drain and PDB exercises. `kind delete cluster && kind create cluster` resets a broken cluster in under a minute |
| Tools | kubectl, helm, k9s, VS Code + WSL extension + Kubernetes/YAML extensions | k9s from day 2 onward, after they have learned the raw kubectl commands |
| Repo location | Clone into `~/k8s-training` inside WSL, never `/mnt/c/...` | Files on the Windows side are slow through WSL and pick up CRLF line endings that break shell scripts |

kind config used by every exercise (`kind.yaml` in the repo):

```yaml
kind: Cluster
apiVersion: kind.x-k8s.io/v1alpha4
nodes:
  - role: control-plane
    extraPortMappings:
      - containerPort: 30080
        hostPort: 30080
      - containerPort: 30081
        hostPort: 30081
  - role: worker
  - role: worker
```

NodePort 30080 on the cluster becomes `http://localhost:30080` in the Windows browser (Lab 3). NodePort 30081 is the Traefik ingress controller installed on day 2 (Lab 4), so every Ingress is reachable at `http://<name>.localhost:30081`; Chrome and Edge resolve `*.localhost` to 127.0.0.1 without editing the hosts file. Host ports keep the same number as the NodePort, so 8080 stays free for `kubectl port-forward` (the app listens on 8080, and that is the port everyone types first). WSL2 forwards `localhost` to Windows, so no WSL networking setup is needed.

### Fallback for a single broken laptop

Admin rights, WSL2 and no proxy mean the local setup should work for all 8. The remaining risks are virtualization disabled in BIOS, or a laptop with 8 GB RAM. For anyone whose preflight still fails by Friday, pair them with a neighbour for the week rather than building a cloud cluster for one person. Have one spare: a cloud VM (Ubuntu, 4 vCPU, 8 GB) with the same install script, reachable over SSH from VS Code Remote.

### Setup instructions to send participants (by Wednesday 2026-10-07)

1. `wsl --install -d Ubuntu-26.04` in an admin PowerShell, reboot, create the Linux user.
2. In Ubuntu, run `setup/install.sh` from the repo. It installs Docker Engine, adds the user to the `docker` group, and installs kubectl, kind, helm and k9s. If Docker Desktop is already installed, quit it or turn off its WSL integration for Ubuntu so the two do not fight over the `docker` socket.
3. Close and reopen the Ubuntu terminal so the group change applies.
4. Optional: `%UserProfile%\.wslconfig` with `memory=8GB` if the laptop has 16 GB.
5. Run `setup/preflight.sh` and send the output. It checks tool versions, creates the kind cluster, pulls the training images, deploys one pod, curls it through `localhost:8080`, and deletes the cluster.

Anyone whose preflight fails gets a 15-minute call before Monday.

Expectation for Monday 09:00: tools installed and preflight passed, but no cluster running. The preflight leaves the `kindest/node` image in Docker's cache, so `kind create cluster --config setup/kind.yaml` on Monday morning takes about a minute and does not need 8 people downloading it over the same Wi-Fi. A fresh cluster on Monday is also safer than one left running since last week, because kind clusters do not always come back cleanly after Windows reboots.

## Training app

Build one small app that every exercise reuses, so time goes into Kubernetes and not into reading new code. It is an ASP.NET Core minimal API on .NET 10 (the current LTS), with Npgsql for Postgres, published as public images on ghcr.io in tags `v1`, `v2` and `v3-broken`:

- `GET /` returns the version, the pod hostname and the value of `Greeting` from configuration (shows load balancing and config).
- `GET /healthz` and `GET /readyz` through ASP.NET Core health checks (`AddHealthChecks`, `MapHealthChecks`), with a Postgres check tagged for readiness only. `/admin/unready` fails readiness, `/admin/hang` fails liveness, `/admin/crash` exits with code 1.
- `/admin/leak` allocates native memory and touches it until the container is OOMKilled. `/admin/leak?managed=true` allocates `byte[]` instead: the .NET GC caps its heap at 75% of the container limit (96 MB under a 128Mi limit) and throws `OutOfMemoryException`, and the container survives. Both verified on kind.
- `GET /work?ms=100` burns CPU on the request thread. A busybox loop calling it scaled the HPA from 2 to 6 pods in about 30 seconds.
- Waits 3 seconds before listening, like a real app warming up, so a rollout without readiness probes visibly drops requests (9 of 339 in testing, 0 with probes and a preStop pause).
- Reads `/config/appsettings.json` if mounted, `/secrets` as one file per key, and `ConnectionStrings__Default`, and writes a visit counter to Postgres (storage and capstone).
- `migrate` as a command-line argument applies the EF Core migrations and exits, so the same image serves as the migration Job in Lab 9.
- `v3-broken` throws on startup when `ConnectionStrings__Default` is missing (CrashLoopBackOff debugging).

Dockerfile is a multi-stage build: `mcr.microsoft.com/dotnet/sdk:10.0` to publish, `mcr.microsoft.com/dotnet/aspnet:10.0` to run. Participants do not need the .NET SDK inside WSL, because the build happens in the SDK image.

Participants never have to build an image for the main path. Building the .NET image and loading it with `kind load docker-image` is a "finished early?" extra in Lab 2.

### .NET behaviour in Kubernetes to teach along the way

These are the points where a .NET developer's assumptions meet Kubernetes. Each one sits inside the lab where it naturally comes up.

| Topic | What happens | Lab |
|---|---|---|
| Port and user | .NET 8+ images listen on 8080 (`ASPNETCORE_HTTP_PORTS`) and include a non-root `app` user (UID 1654) that the Dockerfile switches to. Port 80 in a manifest is the classic first bug | 1 |
| Graceful shutdown | Kubernetes sends SIGTERM, the generic host stops accepting requests and drains within `HostOptions.ShutdownTimeout`. Keep it below `terminationGracePeriodSeconds` (30 s) minus the `preStop` pause | 6 |
| Config mapping | Env var `Section__Key` maps to `Section:Key`. Env vars override `appsettings.json` because of provider order | 5 |
| Config reload | `reloadOnChange: true` on a mounted ConfigMap misses updates, because Kubernetes swaps a symlink and `FileSystemWatcher` does not see it. Set `DOTNET_USE_POLLING_FILE_WATCHER=true` and show the difference | 5 (finished early) |
| Health checks | Liveness must not include the database check, otherwise a Postgres outage restarts every app pod. Readiness can include it | 6 |
| Memory and CPU limits | The GC reads the cgroup limit. `Environment.ProcessorCount` follows the CPU limit, which changes thread pool and Server GC sizing | 7 |
| Chiseled images | `aspnet:10.0-noble-chiseled` is much smaller but has no shell, so `kubectl exec -- sh` fails. Use `kubectl debug` with an ephemeral container instead | finished early |
| Secrets | `Secret` values arrive as env vars or files. `AddKeyPerFile("/secrets")` reads a mounted Secret directory as configuration | 5 |
| Behind an Ingress | The app sees the controller's address and `http` instead of the browser's host and scheme, which breaks redirects and generated links. `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` makes ASP.NET Core read the `X-Forwarded-*` headers | 4 |

## Timing rules

The first version packed 16 labs into 14 hours with 10-minute concept talks. That does not survive contact with 8 people on Windows. This version follows three rules:

- A concept talk gets 20 to 40 minutes, including a live demo and questions. Kubernetes architecture alone needs 40.
- A lab gets about twice the time it takes you to do it alone. Someone always has a typo in YAML indentation, and the group waits for that person or they fall behind.
- Each lab has a "finished early?" section. Fast participants go deeper there instead of the agenda going wider for everyone.

Topics cut from the main path and moved to "finished early?" extras: node drain and PodDisruptionBudget, RBAC, NetworkPolicy, CronJobs, chiseled images with `kubectl debug`, k9s. The Gateway API is not covered beyond a mention in the wrap-up.

## Lab blocks

Each day the talks come first and the labs follow in a long block: one block on Monday, two on Tuesday (one before and one after the break) and two on Wednesday (one before lunch, one at the end of the day). Participants work through the labs in a block at their own pace and take the break inside a block when they want. You walk the room instead of pulling everyone back to the screen between labs.

Two things keep a long block from drifting:

- The lab slide for each block lists checkpoint times. Anyone still on the previous lab at a checkpoint applies its solution from `solutions/` and moves on, so nobody spends the whole block on the first lab and misses the later ones.
- Talks can now be up to two hours before the lab that uses them, so every lab README starts with a short reminder of the concept and never assumes the participant remembers the talk.

## Day 1, Monday 09:00 to 13:00: from container to Deployment

Goal: everyone understands what Kubernetes adds on top of Docker, and has deployed, scaled, updated and rolled back the app.

| Time | Block | Type |
|---|---|---|
| 09:00 | Intro, expectations, everyone's cluster up (`kubectl get nodes` shows 3 nodes) | Setup, 20 min |
| 09:20 | Docker refresher: image vs container, layers, tags, registries. You demo `docker run` of the training app with `-p` and `-e`, `logs`, `exec`, then `docker kill` and ask "who restarts this in production?" | Talk + demo, 25 min |
| 09:45 | What Kubernetes is: control plane and nodes, kubectl and contexts, the API server as a database of objects, the shape of a manifest (`kubectl run hello`, then `kubectl get pod hello -o yaml`), how a pod gets created, namespaces | Talk + demo, 40 min |
| 10:25 | Break | 15 min |
| 10:40 | Labels, selectors, ReplicaSet, Deployment, desired state and the control loop. Demo: kill a pod of a Deployment and watch it come back. Rolling updates | Talk + demo, 20 min |
| 11:00 | Lab block. Lab 1, kubectl and Pods: `get`, `describe`, `explain`, `-o yaml`, `--dry-run=client -o yaml`, namespaces (find the control plane in `kube-system`, `-n` and `-A`, create a namespace and switch to it), create the Pod with `kubectl run`, read it with `kubectl get pod web -o yaml`, delete it (nothing brings it back), generate `pod.yaml` with `--dry-run=client -o yaml`, then `kubectl apply` it and watch it start, with why `apply` beats one-off commands; change the image to `v2` in the file, preview with `kubectl diff`, apply again and see the container restart; map the Docker commands to kubectl, `kubectl port-forward pod/web 8080:8080`. Lab 2, Deployments: generate `deployment.yaml` with a dry run and `apply` it, look at the Deployment, ReplicaSet and pods it created; delete a pod, scale, roll out `v2` and watch the pods get replaced with `kubectl get pods -w`, roll out `v3-broken` and watch it replace every healthy pod, `rollout undo`. Checkpoint: 11:50 start Lab 2 | Hands-on, 105 min |
| 12:45 | Recap, questions | 15 min |

## Day 2, Tuesday 09:00 to 13:00: services, ingress, config and health

Goal: the app is reachable from the browser through a Service and an Ingress, takes its configuration from the cluster, and reports its health.

| Time | Block | Type |
|---|---|---|
| 09:00 | Recap of Monday, questions, recreate yesterday's Deployment with `kubectl apply -f` | 15 min |
| 09:15 | Services: ClusterIP, NodePort, cluster DNS, how a Service finds pods through labels | Talk, 20 min |
| 09:35 | Ingress: how a request gets from the browser through the ingress controller to a Service, IngressClass, host and path rules | Talk, 10 min |
| 09:45 | Lab block. Lab 3, Services: ClusterIP Service called by DNS name from a throwaway pod, NodePort 30080 at `http://localhost:30080`, a `curl` loop showing the pod name change. Lab 4, Ingress: install Traefik with `kubectl apply -f setup/traefik.yaml` (the Traefik Helm chart rendered with `helm template`, so nobody runs Helm before Wednesday), an Ingress that sends `app.localhost` to the app and `v2.localhost` to a second Deployment, both opened at `:30081`, `spec.ingressClassName` vs the older `kubernetes.io/ingress.class` annotation, one real ALB Ingress. Checkpoint: 10:25 start Lab 4 | Hands-on, 65 min |
| 10:50 | Break | 15 min |
| 11:05 | Configuration: ConfigMaps, Secrets, env vars vs mounted files, how this maps onto .NET configuration (`Section__Key`, provider order) | Talk, 20 min |
| 11:25 | Probes: liveness vs readiness, how .NET health checks map onto them, why liveness must not check the database | Talk, 15 min |
| 11:40 | Lab block. Lab 5, Config: `Greeting__Text` as an env var, `appsettings.json` from a ConfigMap, an API key from a Secret at `/secrets`, edit the ConfigMap and see the app ignore it until `kubectl rollout restart`. Lab 6, Probes: run `kubectl rollout restart` with a curl loop and count the failed requests, add both probes, hit `/admin/unready`, `/admin/hang` and `/admin/crash`, add a `preStop` pause and roll out with zero failed requests. Lab 2 has no curl loop and no probes, so this is the first time anyone sees requests fail. Checkpoint: 12:25 start Lab 6 | Hands-on, 70 min |
| 12:50 | Recap, questions | 10 min |

Tuesday has two halves: Services and Ingress with their labs before the break, Configuration and Probes with theirs after it. It is still the densest day, with four talks and four labs. The Gateway API is dropped from the Ingress lab; it stays on the "what we did not cover" slide.

## Day 3, Wednesday 09:00 to 15:00: scaling, debugging, state and packaging

Goal: the app has resource limits and scales under load, participants can debug a broken deployment on their own, the app keeps data in Postgres, and they deploy the whole thing with Helm.

| Time | Block | Type |
|---|---|---|
| 09:00 | Recap of Tuesday, questions. Meanwhile everyone runs `kubectl apply -f setup/metrics-server.yaml` so metrics are ready for Lab 7 | 10 min |
| 09:10 | Requests and limits: what the scheduler does with requests, what happens at a limit (CPU throttled, memory OOMKilled), what Pending means, how the .NET GC and `Environment.ProcessorCount` read the limits | Talk, 15 min |
| 09:25 | Autoscaling: what the HorizontalPodAutoscaler does, where the numbers come from (metrics-server), CPU usage as a percentage of the pod's CPU *request*, min and max replicas, the 5-minute scale-down wait. What it does not do: add nodes (that is the cluster autoscaler) | Talk, 15 min |
| 09:40 | Lab block. Lab 7, Limits and HPA: set requests and a 128Mi memory limit, hit `/admin/leak` and see OOMKilled, then `/admin/leak?managed=true` and see the container survive. Create an HPA (min 2, max 6, 50% CPU), start a load pod calling `/work`, watch `kubectl get hpa -w` and `kubectl top pods`, set `stabilizationWindowSeconds: 30` and watch it scale down. Lab 8, Debugging: five broken manifests (wrong image tag, Service selector mismatch, wrong port, missing Secret key, `v3-broken` CrashLoopBackOff) fixed using only `get`, `describe`, `logs` and events, plus two extras (readiness probe on the wrong path, a CPU request no node can satisfy). Checkpoint: 10:30 start Lab 8 | Hands-on, 110 min |
| 11:30 | Lunch | 30 min |
| 12:00 | Storage and databases: why the container filesystem is lost on restart, PersistentVolumeClaim, Jobs. In production the database is a managed service; StatefulSets exist for running clustered databases and are out of scope | Talk, 20 min |
| 12:20 | Helm: what a chart is, templates and values, `helm install`, `upgrade`, `rollback`, how one chart serves dev and prod. Point out that Tuesday's `setup/traefik.yaml` is a rendered chart | Talk, 10 min |
| 12:30 | Lab block. Lab 9, Database: Postgres as a Deployment (1 replica, `strategy: Recreate`) with a PVC, password from a Secret, EF Core migration as a Job, point the app at Postgres, delete the Postgres pod and confirm the data survived. Lab 10, Helm: render the Traefik chart with `helm template` and compare it with `setup/traefik.yaml`, then turn their own manifests into a chart with `values-dev.yaml` and `values-prod.yaml`. Capstone in pairs: deploy app and Postgres to a fresh namespace from their chart, with config, Secret, probes, limits, an HPA and an Ingress at `http://capstone-<pair>.localhost:30081`, done when `capstone/check.sh` passes. Checkpoints: 13:15 Lab 10, 13:45 capstone | Hands-on, 135 min |
| 14:45 | What they did not cover and where to go next (Gateway API, RBAC, StatefulSets, GitOps, managed clusters). Feedback | 15 min |

Lunch moves 25 minutes earlier than before, because the morning block holds only two labs. The wrap-up is 5 minutes shorter to give the afternoon block the full 135 minutes it needs.

## Repo layout

The repo is at `~/Projects/k8s-training` (to be published as `github.com/amirilovic/k8s-training`). Everything below was tested on a kind cluster on 2026-10-07.

```
k8s-training/
  SETUP.md      participant setup guide (copy of participant-setup.md)
  setup/        kind.yaml, preflight.sh, create-cluster.sh, metrics-server.yaml, traefik.yaml (rendered by render-traefik.sh from traefik-values.yaml)
  app/          ASP.NET Core training app + Dockerfile + README with all endpoints
  labs/NN-.../  README.md per lab; labs/08-debugging has setup.sh, cleanup.sh and broken/1..7.yaml
  solutions/    end state of each lab; 10-helm/web is the full chart; 08-debugging explains each fault
  capstone/     README.md and check.sh (15 checks, run as check.sh <namespace> <host>)
```

Every lab ends with a `check yourself` block of commands with expected output, so participants can confirm they are done without asking. `solutions/` matters more than it looks: a participant who falls behind on Lab 2 should be able to `kubectl apply -f solutions/02-deployments/` and keep up.

## Prep checklist for this week

- [x] Ask the organiser: 8 people, admin rights yes, WSL2 allowed, no proxy, Docker licence unknown (so use Docker Engine). 2026-10-05
- [x] Build the .NET training app (`v1`, `v2`, `v3-broken`), tested on kind. 2026-10-07
- [ ] Push the images to ghcr.io (multi-arch) and make the repo and images public, so participants can pull them in the preflight
- [ ] Write `install.sh`, `preflight.sh`, send setup instructions. Wednesday 2026-10-07
- [ ] Test the full setup on a real Windows machine. Your Mac cannot run WSL2; borrow a Windows laptop or rent an Azure Windows 11 VM with nested virtualization (Dv5 or Ev5 series). Wednesday
- [ ] Collect preflight results from all 8 and do a short call with anyone who failed. Thursday 2026-10-08
- [ ] Write labs 1 to 5 with solutions and "finished early?" extras. Thursday
- [ ] Write labs 6 to 10 (including a pre-patched metrics-server manifest and a Traefik values file with NodePort 30081), the broken manifests, capstone checklist and `check.sh`. Friday 2026-10-09
- [ ] Dry-run day 1 end to end on the Windows machine with a timer. Weekend
- [ ] Short slide deck: architecture diagram, one slide per talk block. Weekend
- [x] Reorder the labs for the lab-block schedule. Folders in `labs/` and `solutions/` renamed, the old probes and limits lab split into `06-probes` and `07-limits-hpa`, every "Before you start" and catch-up command updated, the Ingress lab installs Traefik from `setup/traefik.yaml`, the Helm lab renders the Traefik chart instead of installing it, the Gateway API step and `setup/enable-gateway-api.sh` removed. 2026-10-09
- [x] Render Traefik to plain manifests: `setup/traefik.yaml`, regenerated by `setup/render-traefik.sh`. Tested on kind 2026-10-08: both hostnames and the `kubernetes.io/ingress.class` annotation route through NodePort 30081
- [x] Rebuild the slides for the lab-block schedule. Slide paths already use the new lab folder names. 2026-10-09
- [x] Update AGENDA.md, the lab table in README.md and the lab numbers in capstone/README.md. Links use the new folder names. 2026-10-09
