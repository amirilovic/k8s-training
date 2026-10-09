# Lab 7: Requests, limits and autoscaling

Kubernetes does not know how much CPU and memory your app needs. You tell it with requests and limits, see how .NET reacts to them, and break through the memory limit on purpose. Then you let the HorizontalPodAutoscaler add pods when the app is busy and remove them when it is quiet. About 50 minutes.

## Before you start

- Deployment `web` with probes, ConfigMap `web-config`, Secret `web-secrets` and the NodePort Service from Lab 6 exist, and `curl -s localhost:30080/` answers.
- Work in `~/k8s-training/work`, in your `deployment.yaml` from Lab 6.
- Keep a second Ubuntu tab open. You will watch in one and generate load in the other.

Catch-up: `kubectl apply -f ~/k8s-training/solutions/06-probes/`

metrics-server should already be running; we installed it this morning with:

```bash
kubectl apply -f ~/k8s-training/setup/metrics-server.yaml
```

Check that it works:

```bash
kubectl top nodes
kubectl top pods
```

If `kubectl top` says `metrics not available yet`, wait a minute. metrics-server collects data every 15 seconds and needs a couple of rounds first.

## Part A: requests and limits

### 1. Set requests and limits

Add resources to the container in `deployment.yaml`:

```yaml
          resources:
            requests:          # what the scheduler reserves on a node for this pod
              cpu: 100m        # 100 millicores = a tenth of a CPU core
              memory: 128Mi
            limits:            # the most the container may use
              cpu: "1"
              memory: 128Mi
```

```bash
kubectl apply -f deployment.yaml
kubectl rollout status deployment/web
POD=$(kubectl get pods -l app=web -o jsonpath='{.items[0].metadata.name}')
kubectl port-forward pod/$POD 8080:8080 >/dev/null &
curl -s localhost:8080/api/info; echo
```

Look at two numbers. `processorCount` is now `1`: .NET reads the CPU limit and sizes its thread pool and garbage collector for one core. `gcHeapLimitMb` is `96`: the .NET garbage collector keeps its heap at 75% of the memory limit.

### 2. Run out of memory

```bash
curl -s localhost:8080/admin/leak
sleep 8
kubectl describe pod $POD | grep -A4 'Last State'
```

`Reason: OOMKilled`, `Exit Code: 137`. The app allocated memory outside the .NET heap until it passed 128Mi, and the Linux kernel killed the container. Kubernetes restarted it. The memory limit is a hard wall; going over it is not slowed down, it is killed.

Clean up and give the app room again: set `limits.memory` to `256Mi` in `deployment.yaml` and apply.

```bash
kill %1
kubectl apply -f deployment.yaml
kubectl rollout status deployment/web
```

## Part B: autoscaling

The autoscaler needs the CPU request you just set. It reads usage as a percentage of the request, so without a request it has nothing to compare against.

### 3. Look at the numbers the autoscaler uses

```bash
kubectl top pods -l app=web
kubectl get deployment web -o jsonpath='{.spec.template.spec.containers[0].resources}{"\n"}'
```

An idle pod uses around 10m CPU (10 thousandths of a core) against a request of 100m. The autoscaler reads usage as a percentage of the request: 10m of 100m is 10%.

### 4. Create the HPA

Create `hpa.yaml`:

```yaml
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: web
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: web
  minReplicas: 2
  maxReplicas: 6
  metrics:
    - type: Resource
      resource:
        name: cpu
        target:
          type: Utilization
          averageUtilization: 50   # percent of the pod's CPU request (100m), so 50m per pod
  behavior:
    scaleDown:
      stabilizationWindowSeconds: 30   # default is 300: wait 5 minutes before removing pods
```

```bash
kubectl apply -f hpa.yaml
kubectl get hpa web
```

After about 15 seconds `TARGETS` shows something like `cpu: 9%/50%`: current average usage, then the target. If it shows `<unknown>/50%`, wait for the next metrics round.

The HPA now owns the replica count. If the Deployment had 3 replicas, the HPA brings it down to 2, the minimum, because usage is far below the target.

### 5. Watch

In the first terminal:

```bash
kubectl get hpa web -w
```

### 6. Generate load

In the second terminal, start a pod that calls the app's `/work` endpoint in an endless loop. Each call keeps one CPU busy for 100 ms on whichever pod answers.

```bash
kubectl run load --image=busybox:1.37 --restart=Never -- /bin/sh -c "while true; do wget -q -O /dev/null http://web/work; done"
```

Within about 30 seconds the first terminal shows usage jump far over the target (several hundred percent) and `REPLICAS` go up to 6. In the second terminal, look at the pods and their CPU:

```bash
kubectl get pods -l app=web
kubectl top pods -l app=web
```

The autoscaler computes `desired replicas = current replicas × current usage ÷ target`, rounded up. At 2 pods and 400% against a target of 50%, that is 16, so it stops at the maximum of 6. With 6 pods sharing the same load, usage per pod drops.

### 7. Stop the load and watch it scale down

```bash
kubectl delete pod load --now
```

`--now` matters here. The busybox shell ignores the polite stop signal, so without it Kubernetes waits the full 30-second grace period before killing it.

Usage falls within a minute. Because of `stabilizationWindowSeconds: 30`, the HPA removes pods 30 seconds after the low readings start, and you are back at 2 replicas about a minute after the load stopped. With the default window of 5 minutes, you would still be waiting.

### 8. Read what the HPA decided

```bash
kubectl describe hpa web
```

The `Events` section lists each scaling decision with its reason, for example `New size: 6; reason: cpu resource utilization (percentage of request) above target` and later `New size: 2; reason: All metrics below target`.

## Check yourself

```bash
kubectl get deployment web -o jsonpath='{.spec.template.spec.containers[0].resources.limits.memory}'; echo   # 256Mi
kubectl get hpa web          # TARGETS shows a percentage, not <unknown>; MINPODS 2, MAXPODS 6
kubectl describe hpa web | grep SuccessfulRescale
```

You should see at least one scale-up and one scale-down event.

## Finished early?

- Leak managed memory instead: `curl -s 'localhost:8080/admin/leak?managed=true'` through a port-forward, then `kubectl logs $POD`. The .NET heap hits its 75% limit first and throws `OutOfMemoryException`. The container survives, and `RESTARTS` does not change. In a real app that exception lands in some random request.
- Ask for more than the cluster has: set `requests.cpu` to `"64"` and apply. Run `kubectl get pods` and `kubectl describe pod` on a new pod: `Pending`, with `Insufficient cpu` in the events. No node can reserve 64 cores, so the scheduler never places it. Set it back to `100m`.
- Remove the replica count from your Deployment manifest. While an HPA manages a Deployment, every `kubectl apply` of a manifest with `replicas: 3` resets the count to 3 until the HPA corrects it again. Delete the `replicas:` line so the two do not fight.
- Start a second load pod (`load2`) while the first is running. Does the HPA go above 6? Raise `maxReplicas` to 10 and try again. How many pods fit on your two worker nodes before some stay `Pending`?
- Add a second metric, memory at 80% of the request. The HPA scales on whichever metric asks for more pods.
- The HPA adds pods, not nodes. In a cloud cluster, the cluster autoscaler (or Karpenter on AWS) adds nodes when pods stay `Pending` for lack of room.
