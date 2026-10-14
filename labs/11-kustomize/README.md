# Lab 11 (bonus): one app, two environments with Kustomize

Goal: deploy the training app twice, as dev and as prod, from one set of manifests. Each environment changes only what differs: its namespace, the number of replicas, the image tag and the greeting. Then change dev's config and watch Kustomize roll out new pods by itself.

Duration: about 40 minutes. This lab is not on the schedule: do it when you finish early, or after the training.

## Before you start

- Your cluster runs. Nothing from earlier labs is needed: this lab uses its own namespaces, `web-dev` and `web-prod`.
- Kustomize is built into `kubectl`, so there is nothing to install.
- Create a folder for it:

```bash
mkdir -p ~/k8s-training/work/kustomize/base ~/k8s-training/work/kustomize/overlays/dev ~/k8s-training/work/kustomize/overlays/prod
cd ~/k8s-training/work/kustomize
```

## How it works

```
base/                     what every environment shares
  deployment.yaml
  service.yaml
  kustomization.yaml
overlays/
  dev/kustomization.yaml  dev: the base plus a few changes
  prod/kustomization.yaml prod: the base plus other changes
```

Every folder has a `kustomization.yaml` that says which files belong to it and what to change. There are no templates and no placeholders: the base is plain YAML that you could apply as it is.

## Steps

### 1. Write the base

`base/deployment.yaml`. The greeting comes from a ConfigMap called `web-settings`, read as environment variables:

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: web
spec:
  replicas: 2
  selector:
    matchLabels:
      app: web
  template:
    metadata:
      labels:
        app: web
    spec:
      containers:
        - name: web
          image: ghcr.io/amirilovic/k8s-training-app:v1
          ports:
            - containerPort: 8080
          envFrom:
            - configMapRef:
                name: web-settings    # every key becomes an env var: Greeting__Text
          readinessProbe:
            httpGet:
              path: /readyz
              port: 8080
```

`base/service.yaml`:

```yaml
apiVersion: v1
kind: Service
metadata:
  name: web
spec:
  selector:
    app: web
  ports:
    - port: 80
      targetPort: 8080
```

`base/kustomization.yaml`:

```yaml
resources:
  - deployment.yaml
  - service.yaml
configMapGenerator:          # Kustomize writes the ConfigMap for you
  - name: web-settings
    literals:
      - Greeting__Text=Hello from the base
```

See what Kustomize makes of it:

```bash
kubectl kustomize base
```

`kubectl kustomize` only prints the result, it applies nothing. Look at the ConfigMap: its name is `web-settings-` plus a hash, for example `web-settings-57f8c4g4m5`. Now look at the Deployment: Kustomize changed its `configMapRef` to the same name with the hash. Step 5 shows why.

### 2. Write the dev overlay

`overlays/dev/namespace.yaml`:

```yaml
apiVersion: v1
kind: Namespace
metadata:
  name: web-dev
```

`overlays/dev/kustomization.yaml`:

```yaml
resources:
  - ../../base               # start from the base
  - namespace.yaml
namespace: web-dev           # put every object in this namespace
replicas:
  - name: web
    count: 1
configMapGenerator:
  - name: web-settings
    behavior: merge          # change keys of the base's ConfigMap
    literals:
      - Greeting__Text=Hello from dev
```

```bash
kubectl kustomize overlays/dev
```

Every object now has `namespace: web-dev`, the Deployment has `replicas: 1`, and the greeting is `Hello from dev`. You did not copy or edit the base.

### 3. Write the prod overlay

`overlays/prod/namespace.yaml` is the same as dev's, with the name `web-prod`.

`overlays/prod/kustomization.yaml`:

```yaml
resources:
  - ../../base
  - namespace.yaml
namespace: web-prod
replicas:
  - name: web
    count: 3
images:                       # change the tag without touching deployment.yaml
  - name: ghcr.io/amirilovic/k8s-training-app
    newTag: v2
configMapGenerator:
  - name: web-settings
    behavior: merge
    literals:
      - Greeting__Text=Hello from prod
```

### 4. Deploy both

```bash
kubectl apply -k overlays/dev
kubectl apply -k overlays/prod
kubectl get deployment web -n web-dev
kubectl get deployment web -n web-prod
```

`-k` means "build this folder with Kustomize, then apply the result". dev runs 1 pod, prod runs 3. Call each one:

```bash
kubectl -n web-dev port-forward svc/web 8081:80
```

In another tab: `curl localhost:8081/` answers `Hello from dev | version: v1`. Stop the port-forward with Ctrl+C and do the same for prod with `kubectl -n web-prod port-forward svc/web 8082:80`: `Hello from prod | version: v2`.

### 5. Change dev's config

In `overlays/dev/kustomization.yaml`, change the greeting to `Hello again from dev`. Before applying, ask what would change:

```bash
kubectl diff -k overlays/dev
```

The diff shows a new ConfigMap with a new hash in its name, and the Deployment's `configMapRef` switching to that name. Apply it and watch:

```bash
kubectl apply -k overlays/dev
kubectl -n web-dev get pods -w
```

The pod is replaced on its own, and the new one says `Hello again from dev`. Stop the watch with Ctrl+C.

Compare this with Lab 5: there, editing the ConfigMap changed the file in the pod, but the app kept the old value until you ran `kubectl rollout restart`. Here, new config means a new ConfigMap name, the name is part of the pod template, and a changed pod template is a rollout. You also get rollback for config: `kubectl -n web-dev rollout undo deployment/web` goes back to the pods that use the old ConfigMap.

`kubectl -n web-dev get configmaps` still lists the old ConfigMap. `apply` does not delete objects that left your files. That is fine here, and it is what makes the rollback work.

## Check yourself

```bash
kubectl get deployment web -n web-dev -o jsonpath='{.spec.replicas} {.spec.template.spec.containers[0].image}{"\n"}'    # 1 ...:v1
kubectl get deployment web -n web-prod -o jsonpath='{.spec.replicas} {.spec.template.spec.containers[0].image}{"\n"}'   # 3 ...:v2
```

Clean up both environments, including their namespaces:

```bash
kubectl delete -k overlays/dev
kubectl delete -k overlays/prod
```

## Finished early?

- Give prod resource limits without touching the base. Add a patch to `overlays/prod/kustomization.yaml`:

  ```yaml
  patches:
    - target:
        kind: Deployment
        name: web
      patch: |-
        - op: add
          path: /spec/template/spec/containers/0/resources
          value:
            requests: { cpu: 100m, memory: 128Mi }
            limits: { memory: 256Mi }
  ```

  Run `kubectl kustomize overlays/prod` to check, then apply.
- Label what an environment creates. Add `labels: [{pairs: {env: prod}}]` to the prod overlay and find it with `kubectl get deploy,svc,cm -n web-prod -l env=prod`. The pods do not get it: add `includeTemplates: true` next to `pairs` to label the pod template too.
- Add a third environment, `staging`, with 2 replicas and image `v1`, by copying the dev overlay. Only the overlay is new; the base stays the same for all three.
