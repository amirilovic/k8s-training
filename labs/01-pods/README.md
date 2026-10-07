# Lab 1: kubectl, namespaces and Pods

You get to know `kubectl`, find the control plane from the architecture talk, create your own namespace, and run the training app as a single Pod. You will see that a Pod on its own is not much better than `docker run`: delete it and it is gone. About 50 minutes.

## Before you start

- Your cluster runs: `kubectl get nodes` shows `training-control-plane`, `training-worker` and `training-worker2`, all `Ready`.
- You are in the Ubuntu terminal, in your home directory (`cd ~`), and the repo is at `~/k8s-training`.
- Create a folder for your own files. You write every YAML file in this training there:

```bash
mkdir -p ~/k8s-training/work && cd ~/k8s-training/work
```

## Steps

### 1. Ask the cluster what it has

```bash
kubectl get nodes -o wide
kubectl get namespaces
```

A namespace is a folder for objects. `kube-system` holds the cluster's own components. `default` is where objects go when you name no namespace.

### 2. Find the control plane

```bash
kubectl get pods -n kube-system
```

You should recognise the parts from the architecture talk:

| Pod name starts with | What it is |
|---|---|
| `kube-apiserver` | The API every `kubectl` command talks to |
| `etcd` | The database behind the API server |
| `kube-scheduler` | Picks a node for each new pod |
| `kube-controller-manager` | Runs the control loops (for example, "keep 3 replicas") |
| `kube-proxy` | Runs on every node, implements Services |
| `coredns` | DNS inside the cluster |

Now list pods in every namespace at once:

```bash
kubectl get pods -A
```

`-n <name>` picks one namespace, `-A` means all of them.

### 3. Create your own namespace and make it the default

```bash
kubectl create namespace training
kubectl config set-context --current --namespace training
kubectl config get-contexts
```

The `NAMESPACE` column now shows `training`. Every command from here on, and in all later labs, runs there unless you add `-n`.

### 4. Look up what a field means

You do not need to remember the YAML structure. The cluster can explain it:

```bash
kubectl explain pod
kubectl explain pod.spec.containers
kubectl explain pod.spec.containers.ports
```

### 5. Generate YAML instead of writing it

Writing YAML from memory leads to indentation errors. Let `kubectl` write the first draft:

```bash
kubectl run web --image=ghcr.io/amirilovic/k8s-training-app:v1 --port=8080 --dry-run=client -o yaml
```

`--dry-run=client` means "do not create anything, just show me". Save a cleaned-up version as `pod.yaml`:

```yaml
apiVersion: v1
kind: Pod
metadata:
  name: web
  labels:
    app: web              # a label: a key-value tag that other objects use to find this pod
spec:
  containers:
    - name: web
      image: ghcr.io/amirilovic/k8s-training-app:v1
      ports:
        - containerPort: 8080   # documentation only: the app listens on 8080, not 80
```

### 6. Run it

```bash
kubectl apply -f pod.yaml
kubectl get pods -o wide
```

`-o wide` shows the pod's IP and the node it runs on. The scheduler chose that node.

### 7. The Docker commands you already know

| Docker | kubectl |
|---|---|
| `docker run -p 8080:8080 image` | `kubectl apply -f pod.yaml`, then `kubectl port-forward pod/web 8080:8080` |
| `docker ps` | `kubectl get pods` |
| `docker logs web` | `kubectl logs web` |
| `docker exec web env` | `kubectl exec web -- env` |
| `docker inspect web` | `kubectl describe pod web` or `kubectl get pod web -o yaml` |
| `docker rm -f web` | `kubectl delete pod web` |

Try them:

```bash
kubectl logs web
kubectl exec web -- env
kubectl exec web -- id
kubectl describe pod web
```

The logs show the app starting and what it found (no config file, no secrets, no database). `id` shows `uid=1654(app)`: the .NET image runs as a non-root user. At the bottom of `describe`, the `Events` list what Kubernetes did: scheduled, pulled, created, started. Events are the first place to look when something goes wrong.

### 8. Open the app in your browser

```bash
kubectl port-forward pod/web 8080:8080
```

Leave it running and open `http://localhost:8080` in the Windows browser. You should see:

```
Hello from Kubernetes | version: v1 | pod: web
```

Also open `http://localhost:8080/api/info`. Stop the port-forward with Ctrl+C.

Port-forward is a debugging tunnel from your machine to one pod. It is not how users reach an app. That comes with Services in Lab 3.

### 9. Delete the pod

```bash
kubectl delete pod web
kubectl get pods
```

It is gone, and nothing brings it back. A bare Pod has no one watching over it. In Lab 2 a Deployment takes that job.

## Check yourself

```bash
kubectl config view --minify -o jsonpath='{..namespace}'; echo
```

Expected: `training`

```bash
kubectl apply -f pod.yaml && kubectl wait --for=condition=Ready pod/web --timeout=60s
kubectl get pod web --show-labels
```

Expected: `pod/web condition met`, and the pod has the label `app=web`.

Clean up before Lab 2:

```bash
kubectl delete pod web
```

## Finished early?

- Change `containerPort` in `pod.yaml` to `9090` and run `kubectl apply -f pod.yaml` again. Read the error: almost every field of a running Pod is fixed, and the image is one of the few exceptions. To change anything else you delete and recreate the Pod. That is one more reason to use Deployments.
- Look at everything Kubernetes added to your 15-line file: `kubectl get pod web -o yaml`. Find `status.podIP`, `spec.nodeName` and `status.conditions`.
- Get a shell with tools next to the app without changing its image: `kubectl debug -it web --image=busybox:1.37 --target=web --profile=general -- sh`. Inside, run `wget -qO- localhost:8080/api/info` and `ps`. This is how you debug images that have no shell at all, such as the small "chiseled" .NET images (`mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled`).
