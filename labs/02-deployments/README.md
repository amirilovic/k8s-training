# Lab 2: Deployments, rolling updates and rollback

You let a Deployment run the app: it keeps 3 copies alive, replaces pods that die, and rolls out new versions one pod at a time. You also release a broken version, watch it take the app down, and roll it back. About 55 minutes.

## Before you start

- Lab 1 done: your namespace is `training` (`kubectl config view --minify -o jsonpath='{..namespace}'` prints `training`).
- No pod named `web` left over: `kubectl delete pod web --ignore-not-found`.
- Work in `~/k8s-training/work`.
- Open a second Ubuntu tab (Windows Terminal: Ctrl+Shift+T, then pick Ubuntu 26.04). You use it to watch things change.

Catch-up: `kubectl apply -f ~/k8s-training/solutions/02-deployments/`

## Steps

### 1. Generate a Deployment

Use a dry run, as at the end of Lab 1:

```bash
kubectl create deployment web --image=ghcr.io/amirilovic/k8s-training-app:v1 --replicas=3 --port=8080 --dry-run=client -o yaml > deployment.yaml
```

Edit `deployment.yaml` until it matches this. Remove both `creationTimestamp` lines, `strategy: {}`, `resources: {}` and `status: {}`, and rename the container from `k8s-training-app` to `web`; later steps refer to it by that name.

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: web
spec:
  replicas: 3                 # how many pods you want
  selector:
    matchLabels:
      app: web                # which pods belong to this Deployment
  template:                   # the pod to create, same fields as your pod.yaml from Lab 1
    metadata:
      labels:
        app: web              # must match the selector above
    spec:
      containers:
        - name: web
          image: ghcr.io/amirilovic/k8s-training-app:v1
          ports:
            - containerPort: 8080
```

### 2. Apply it and look at what it created

In the second tab, watch the pods:

```bash
kubectl get pods -w
```

In the first tab:

```bash
kubectl apply -f deployment.yaml
kubectl rollout status deployment/web
kubectl get deployment,replicaset,pods -o wide
```

The watch shows 3 pods appear and start. You wrote one object and got three kinds: the Deployment created a ReplicaSet, and the ReplicaSet created 3 pods. The pod names are `<deployment>-<replicaset hash>-<random>`. The `NODE` column shows where the scheduler put each pod. Usually they are spread over both workers, but nothing forces that.

From now on you change the app by editing `deployment.yaml` and running `apply` again. Change the image in the file, apply, and Kubernetes rolls out the new version.

### 3. Delete a pod and watch it come back

In the second tab:

```bash
kubectl get pods -w
```

In the first tab, delete one pod (use a name from your list):

```bash
kubectl delete pod web-xxxxxxxxxx-yyyyy
```

The watch shows the old pod `Terminating` and a new one appearing within a second. The ReplicaSet saw 2 pods where you asked for 3 and fixed it. That is the control loop from the talk.

### 4. Scale

```bash
kubectl scale deployment web --replicas=5
kubectl get pods -o wide
```

Then put `replicas: 3` back by applying your file again:

```bash
kubectl apply -f deployment.yaml
```

The file is the truth. `kubectl scale` is handy, but the next `apply` wins.

### 5. Roll out v2

In the second tab, watch the pods:

```bash
kubectl get pods -w
```

In the first tab:

```bash
kubectl set image deployment/web web=ghcr.io/amirilovic/k8s-training-app:v2
kubectl rollout status deployment/web
```

`web=` is the container name inside the pod. The watch shows the rolling update: a new pod starts, and once it runs an old one terminates, one at a time, until all 3 are new. The new pods belong to a new ReplicaSet, so their names have a different hash.

Update `image:` in `deployment.yaml` to `v2` too, so your file matches the cluster.

### 6. Look at the history

```bash
kubectl rollout history deployment/web
kubectl get replicasets
```

Every rollout created a new ReplicaSet. The old one is kept with 0 pods, so a rollback is quick.

### 7. Release a broken version

```bash
kubectl set image deployment/web web=ghcr.io/amirilovic/k8s-training-app:v3-broken
kubectl annotate deployment/web kubernetes.io/change-cause="release v3-broken" --overwrite
kubectl get pods -w
```

Within a minute the pods show `Error`, then `CrashLoopBackOff`. Every healthy v2 pod was replaced by one that crashes, so the app is down.

Why did the rollout not stop? Kubernetes only knows that the container started. The broken pod started, looked fine for half a second, and the rollout moved on. On Tuesday you teach Kubernetes to check the app itself.

### 8. Find out why

```bash
kubectl get pods
kubectl logs web-xxxxxxxxxx-yyyyy
kubectl describe pod web-xxxxxxxxxx-yyyyy
```

The log says:

```
FATAL: ConnectionStrings:Default is not set. Set the ConnectionStrings__Default environment variable.
```

In `describe`, `Last State: Terminated, Reason: Error, Exit Code: 1` and a growing `Restart Count`. `CrashLoopBackOff` means Kubernetes keeps restarting the container and waits longer each time (10 s, 20 s, 40 s, up to 5 minutes). If `kubectl logs` shows nothing because the container is between restarts, add `--previous`.

### 9. Roll back

```bash
kubectl rollout undo deployment/web
kubectl rollout status deployment/web
kubectl rollout history deployment/web
```

`undo` goes back one revision, to v2. The watch shows healthy pods come back. Stop it with Ctrl+C.

## Check yourself

```bash
kubectl get deployment web
kubectl get deployment web -o jsonpath='{.spec.template.spec.containers[0].image}'; echo
```

Expected: `READY 3/3`, and the image ends in `:v2`.

## Finished early?

- Roll back to a specific revision: `kubectl rollout history deployment/web`, then `kubectl rollout undo deployment/web --to-revision=1`. Check the image with `kubectl get deployment web -o wide`.
- Change how fast a rollout goes. Add this under `spec:` in `deployment.yaml`, apply, and roll out again while `kubectl get pods -w` runs:

  ```yaml
  strategy:
    rollingUpdate:
      maxSurge: 3          # create up to 3 extra pods at once
      maxUnavailable: 0    # never go below 3 running pods
  ```

- Build your own version of the app. The source is in `~/k8s-training/app`:

  ```bash
  docker build -t k8s-training-app:mine --build-arg APP_VERSION=mine ~/k8s-training/app
  kind load docker-image k8s-training-app:mine --name training
  kubectl set image deployment/web web=k8s-training-app:mine
  ```

  `kind load` copies the image from your Docker into the cluster's nodes, so no registry is needed. Change the default greeting in `app/appsettings.json` first, then open the app with `kubectl port-forward deployment/web 8080:8080` and `http://localhost:8080` to see your change.
