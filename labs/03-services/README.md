# Lab 3: Services and cluster DNS

Pods come and go, and every new pod gets a new IP address. A Service gives a group of pods one stable name and address, and spreads requests over them. You build two: first one reachable only inside the cluster, then one reachable from your browser. About 40 minutes.

## Before you start

- Your Deployment `web` from Lab 2 runs with 3 ready pods (`kubectl get deployment web`).
- Work in `~/k8s-training/work`.

Catch-up: `kubectl apply -f ~/k8s-training/solutions/02-deployments/`

## Steps

### 1. Find the pods by their label

A Service does not list pods by name. It selects them by label, the same way the Deployment does:

```bash
kubectl get pods --show-labels
kubectl get pods -l app=web -o wide
```

Note the pod IPs. They change whenever a pod is replaced, so nothing should depend on them.

### 2. Generate a ClusterIP Service

```bash
kubectl expose deployment web --port=80 --target-port=8080 --dry-run=client -o yaml
```

Save a cleaned-up version as `service.yaml`:

```yaml
apiVersion: v1
kind: Service
metadata:
  name: web               # also the DNS name: http://web
spec:
  selector:
    app: web              # send traffic to pods with this label
  ports:
    - port: 80            # the port clients use
      targetPort: 8080    # the port the container listens on
```

Without a `type`, a Service is `ClusterIP`: an address that only works inside the cluster.

```bash
kubectl apply -f service.yaml
kubectl get service web
```

### 3. See which pods it found

```bash
kubectl get endpointslices -l kubernetes.io/service-name=web
```

The `ENDPOINTS` column lists the pod IPs from step 1, on port 8080. If this list is empty, the selector matches no pods. That is the most common Service bug, and you will meet it again in Lab 7.

### 4. Call the Service from inside the cluster

Start a throwaway pod with basic tools and call the Service by name:

```bash
kubectl run tmp --rm -i --restart=Never --image=busybox:1.37 -- sh -c 'for i in 1 2 3 4 5 6; do wget -qO- http://web; done'
```

`--rm` deletes the pod when the command ends. The answers come from different pods: the Service spreads requests over all 3.

The short name `web` works because both pods are in the same namespace. The full name works from anywhere in the cluster:

```bash
kubectl run tmp --rm -i --restart=Never --image=busybox:1.37 -- wget -qO- http://web.training.svc.cluster.local
kubectl run tmp --rm -i --restart=Never --image=busybox:1.37 -- nslookup web.training.svc.cluster.local
```

`nslookup` shows that the name resolves to the Service's `CLUSTER-IP` from step 2, not to a pod. The pattern is `<service>.<namespace>.svc.cluster.local`. Your .NET apps use these names in connection strings and `HttpClient` base addresses.

### 5. Scale and watch the endpoints follow

```bash
kubectl scale deployment web --replicas=5
kubectl get endpointslices -l kubernetes.io/service-name=web
kubectl scale deployment web --replicas=3
```

The Service picks up new pods and drops removed ones on its own.

### 6. Make it reachable from your browser with a NodePort

A `NodePort` Service also opens a port on every node. Your kind cluster publishes node port 30080 on your machine. Change `service.yaml`:

```yaml
apiVersion: v1
kind: Service
metadata:
  name: web
spec:
  type: NodePort
  selector:
    app: web
  ports:
    - port: 80
      targetPort: 8080
      nodePort: 30080     # must be between 30000 and 32767
```

```bash
kubectl apply -f service.yaml
kubectl get service web
```

`PORT(S)` now shows `80:30080/TCP`. Open `http://localhost:30080` in the Windows browser.

### 7. Watch the load balancing

Refresh the browser a few times. The pod name may stay the same: the browser keeps its connection open, and a Service balances connections, not requests. A loop with `curl` opens a new connection every time:

```bash
for i in $(seq 1 10); do curl -s localhost:30080/; done
```

Now the pod names change.

## Check yourself

```bash
kubectl get service web
kubectl get endpointslices -l kubernetes.io/service-name=web
for i in $(seq 1 6); do curl -s localhost:30080/; done | grep -o 'pod: [^ ]*' | sort | uniq -c
```

Expected: `web` has type `NodePort` with `80:30080/TCP`, the endpoint slice lists 3 addresses on port 8080, and the curl loop shows more than one pod name.

## Finished early?

- Break the selector on purpose: change `app: web` to `app: webb` in `service.yaml`, apply, and look at the endpoints and the browser. Put it back.
- Point `targetPort` at `80` instead of `8080`, apply, and run the curl loop. Read the error. Put it back.
- Try k9s, a terminal UI for everything you have done with `kubectl` so far. Run `k9s`, type `:pods` or `:svc` and Enter, use arrow keys and `d` (describe), `l` (logs), `?` for help, `:q` to quit.
