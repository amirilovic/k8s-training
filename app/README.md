# Training app

A small ASP.NET Core (.NET 10) web app that every lab uses. It is built to be broken on purpose: each endpoint below exists so you can watch how Kubernetes reacts.

Images: `ghcr.io/amirilovic/k8s-training-app` with the tags `v1`, `v2` and `v3-broken`. `v1` and `v2` differ only in the version they report. `v3-broken` exits on startup because it requires a database connection string.

The app listens on port 8080 and waits 3 seconds before accepting requests, like a real app warming up caches.

## Endpoints

| Endpoint | What it does |
|---|---|
| `GET /` | One line: greeting, version, pod name, and the visit counter when a database is configured |
| `GET /api/info` | JSON with version, pod, greeting, CPUs and GC heap limit as .NET sees them, masked API key |
| `GET /healthz` | Liveness: fails after `/admin/hang` |
| `GET /readyz` | Readiness: fails after `/admin/unready`, or when the database is configured but unreachable |
| `GET /work?ms=100` | Burns CPU for the given milliseconds. The HPA lab sends a stream of these |
| `/admin/unready`, `/admin/ready` | Turn readiness off and on. The process keeps running |
| `/admin/hang` | Liveness starts failing, as if the app were deadlocked |
| `/admin/crash` | Exits with code 1 |
| `/admin/leak` | Allocates native memory until the container is OOMKilled |
| `/admin/leak?managed=true` | Allocates `byte[]` until .NET throws `OutOfMemoryException`. The GC heap limit is 75% of the container memory limit, so the container survives |

Admin endpoints accept GET as well as POST, so they also work from a browser. The image has no `curl`; call the app from your terminal through `kubectl port-forward` or a NodePort.

## Configuration

| Setting | Environment variable | Default |
|---|---|---|
| Greeting | `Greeting__Text` | `Hello from Kubernetes` |
| API key (shown masked) | `Secrets__ApiKey` | not set |
| Database | `ConnectionStrings__Default` | not set |
| Startup delay | `App__StartupDelaySeconds` | `3` |

Sources, from lowest to highest priority: `appsettings.json` in the image, `/config/appsettings.json` (mount a ConfigMap there), one file per key in `/secrets` (mount a Secret there), environment variables, command-line arguments. A mounted ConfigMap is reloaded when it changes, but only with `DOTNET_USE_POLLING_FILE_WATCHER=true`; Kubernetes replaces a symlink, which .NET's file watcher does not see.

The database settings are read at startup. Restart the pods after adding a connection string.

## Migrations

`dotnet TrainingApp.dll migrate` applies the EF Core migrations and exits. In Kubernetes that is the same image with `args: ["migrate"]`, run as a Job. It retries for 60 seconds while the database starts.

## Building it yourself

```bash
docker build -t k8s-training-app:mine --build-arg APP_VERSION=mine app
kind load docker-image --name training k8s-training-app:mine
```

The Dockerfile is a multi-stage build: the .NET SDK image compiles the app, and only the published output goes into the smaller ASP.NET Core runtime image. You do not need the .NET SDK installed.

Publishing the official tags (trainer only, multi-architecture):

```bash
for v in v1 v2; do
  docker buildx build --platform linux/amd64,linux/arm64 --build-arg APP_VERSION=$v \
    -t ghcr.io/amirilovic/k8s-training-app:$v --push app
done
docker buildx build --platform linux/amd64,linux/arm64 --build-arg APP_VERSION=v3-broken \
  --build-arg REQUIRE_DATABASE=true -t ghcr.io/amirilovic/k8s-training-app:v3-broken --push app
```
