# Lab 9: Helm

Goal: install someone else's application from a Helm chart, then package your own manifests as a chart and deploy it twice, as dev and as prod, from the same templates.

Duration: 30 minutes.

## Before you start

You need your manifests from Labs 5 and 6 in `~/k8s-training/work/`: `deployment.yaml`, `service.yaml`, `configmap.yaml` and `secret.yaml`. If you are missing any, copy them from the solutions:

```bash
cp ~/k8s-training/solutions/05-probes-limits/*.yaml ~/k8s-training/work/
```

Check that Helm is installed:

```bash
helm version
```

## Steps

### Part A: use a chart

### 1. Install Traefik, an ingress controller

Lab 10 needs an ingress controller, the component that receives HTTP traffic from outside the cluster and forwards it to your Services. We install Traefik from its official chart.

```bash
helm repo add traefik https://traefik.github.io/charts
helm repo update
helm install traefik traefik/traefik --version 41.6.1 \
  -n traefik --create-namespace \
  -f ~/k8s-training/setup/traefik-values.yaml
```

The parts of that command:

- `traefik` (first) is the release name, your name for this installation.
- `traefik/traefik` is the chart: repository `traefik`, chart `traefik`.
- `--version` pins the chart version, so everyone gets the same thing.
- `-f` passes a values file that overrides the chart's defaults. Open `~/k8s-training/setup/traefik-values.yaml`: it changes a few settings. The chart has hundreds; see them with `helm show values traefik/traefik --version 41.6.1 | less`.

### 2. Look at what Helm created

```bash
helm list -n traefik
helm get values traefik -n traefik
kubectl get all -n traefik
```

One `helm install` created a Deployment, a Service of type NodePort on port 30081, and RBAC objects you did not have to write (`kubectl get all` does not show those). Open `http://test.localhost:30081` in the browser. You get `404 page not found` from Traefik: it is running, but no Ingress tells it where to send traffic yet. That comes in Lab 10.

### Part B: write a chart

### 3. Create the chart skeleton

```bash
mkdir -p ~/k8s-training/work/chart/web/templates
cd ~/k8s-training/work/chart
cp ../deployment.yaml ../service.yaml ../configmap.yaml ../secret.yaml web/templates/
```

Create `web/Chart.yaml`:

```yaml
apiVersion: v2
name: web
description: The training app
version: 0.1.0
appVersion: "v1"
```

A chart is a directory with `Chart.yaml`, a `values.yaml` and a `templates/` folder. Right now the templates are plain YAML, which already works, but always produces the same result.

### 4. Clean up the copied manifests

Two of the copied files would clash with what is already running in namespace `training`:

- `service.yaml`: remove `type: NodePort` and the `nodePort: 30080` line. Only one Service in the whole cluster can own NodePort 30080. A plain ClusterIP Service is enough; you reach it with `port-forward` here and through Traefik in Lab 10.
- `secret.yaml`: remove the `ConnectionStrings__Default` line and keep only `Secrets__ApiKey`. The chart installs into a new namespace without a database. With a connection string, the app's readiness check would fail and no pod would become ready.

### 5. Turn the fixed values into parameters

Create `web/values.yaml` with the defaults:

```yaml
image:
  tag: v1
replicas: 2
greeting: Hello from Helm
```

Now replace the hard-coded values in the templates with references to these values.

In `templates/deployment.yaml`:

```yaml
spec:
  replicas: {{ .Values.replicas }}
  ...
          image: ghcr.io/amirilovic/k8s-training-app:{{ .Values.image.tag }}
```

In `templates/configmap.yaml`:

```yaml
data:
  appsettings.json: |
    {
      "Greeting": {
        "Text": {{ .Values.greeting | quote }}
      }
    }
```

`{{ ... }}` is a Go template expression. `.Values` holds everything from `values.yaml`, merged with any values files and `--set` flags you pass. `| quote` wraps the value in double quotes, so a greeting with special characters still produces valid JSON.

### 6. Render it without installing

```bash
helm lint web
helm template web ./web
```

`helm template` prints the YAML Helm would send to the cluster. Check that the replica count, image and greeting are filled in. This is the fastest way to debug a template.

### 7. One chart, two environments

Create `web/values-dev.yaml`:

```yaml
replicas: 1
greeting: Hello from dev
```

and `web/values-prod.yaml`:

```yaml
replicas: 3
greeting: Hello from prod
```

These only contain what differs from `values.yaml`. Compare the rendered output:

```bash
helm template web ./web -f web/values-dev.yaml | grep -e replicas -e Text
helm template web ./web -f web/values-prod.yaml | grep -e replicas -e Text
```

### 8. Install both

Each environment gets its own namespace:

```bash
helm install web ./web -n web-dev --create-namespace -f web/values-dev.yaml --wait
helm install web ./web -n web-prod --create-namespace -f web/values-prod.yaml --wait
helm list -A
kubectl get deployments -n web-dev
kubectl get deployments -n web-prod
```

Dev runs 1 pod, prod runs 3. Call the dev release:

```bash
kubectl port-forward -n web-dev svc/web 8080:80
```

and open `http://localhost:8080`, or `curl http://localhost:8080/` in another terminal. You see `Hello from dev`. Stop the port-forward with Ctrl+C.

Why not install into `training`? Helm refuses to take over objects it did not create. `helm install web ./web -n training` fails with `Secret "web-secrets" in namespace "training" exists and cannot be imported into the current release`.

### 9. Upgrade and roll back

Release a new version to dev:

```bash
helm upgrade web ./web -n web-dev -f web/values-dev.yaml --set image.tag=v2 --wait
helm history web -n web-dev
```

`--set` overrides one value on the command line. Start the port-forward again and check: the response now says `version: v2`. Then roll back to the first revision:

```bash
helm rollback web 1 -n web-dev --wait
helm history web -n web-dev
kubectl get deployment web -n web-dev -o jsonpath='{.spec.template.spec.containers[0].image}{"\n"}'
```

Helm keeps every revision, so a rollback brings back the exact manifests of that revision, not only the image tag.

## Check yourself

```bash
helm list -A                                # traefik, web (web-dev), web (web-prod), all deployed
kubectl get deployment web -n web-dev       # 1/1
kubectl get deployment web -n web-prod      # 3/3
helm history web -n web-dev                 # 3 revisions, the last one a rollback to 1
```

## Finished early?

- Open the reference chart in `~/k8s-training/solutions/09-helm/web/`. It adds an Ingress, an optional HPA and an optional Postgres database, and runs the migration Job as a Helm hook after every install and upgrade (`helm.sh/hook: post-install,post-upgrade`). This is the chart you can start the capstone from.
- In the reference chart, the Deployment has `checksum/config` and `checksum/secret` annotations. Find out what they do: change only the greeting with `helm upgrade` in your chart and in the reference chart, and compare whether the pods are replaced.
- Use `{{ .Release.Name }}` instead of the fixed name `web` in your templates, so the same chart can be installed twice in one namespace.
- Try `helm upgrade --install`. It installs when the release does not exist and upgrades when it does, which is what CI pipelines usually run.
- Remove the dev release with `helm uninstall web -n web-dev` and check with `kubectl get all -n web-dev` that everything it created is gone.
