# Lab 6: Probes

Kubernetes only knows that your process runs. It does not know whether the app is ready for traffic or whether it is stuck. You tell it with probes, then break the app on purpose to see what Kubernetes does with that information. At the end you repeat yesterday's rollout and it drops no requests. About 25 minutes.

## Before you start

- Deployment `web`, ConfigMap `web-config`, Secret `web-secrets` and the NodePort Service from Lab 5 exist, and `curl -s localhost:30080/` answers.
- Work in `~/k8s-training/work`, in your `deployment.yaml` from Lab 5.
- Keep a second Ubuntu tab open for watching.

Catch-up: `kubectl apply -f ~/k8s-training/solutions/05-config/`

The app has health endpoints, plus admin endpoints that break it on purpose:

| Endpoint | What it does |
|---|---|
| `/healthz` | Liveness: is the process still working? Returns 200 or 503 |
| `/readyz` | Readiness: should this pod get traffic? Returns 200 or 503 |
| `/admin/unready`, `/admin/ready` | Make `/readyz` fail, and fix it again |
| `/admin/hang` | Make `/healthz` fail, like a deadlocked app whose process still runs |
| `/admin/crash` | Exit the process with code 1 |

## Steps

### 1. Add a readiness and a liveness probe

Add both probes to the container in `deployment.yaml`, at the same level as `ports`:

```yaml
          readinessProbe:          # failing: take the pod out of the Service, do not restart it
            httpGet:
              path: /readyz
              port: 8080
            periodSeconds: 5       # check every 5 seconds
            failureThreshold: 2    # 2 failures in a row count as not ready
          livenessProbe:           # failing: restart the container
            httpGet:
              path: /healthz
              port: 8080
            periodSeconds: 5
            failureThreshold: 3
```

```bash
kubectl apply -f deployment.yaml
kubectl rollout status deployment/web
```

During this rollout, the new pods show `0/1` for about 3 seconds after they start: the app warms up, and the readiness probe keeps traffic away until it answers.

### 2. Take one pod out of rotation

In the second tab:

```bash
kubectl get pods -w
```

In the first tab, pick one pod and open a tunnel to it in the background:

```bash
POD=$(kubectl get pods -l app=web -o jsonpath='{.items[0].metadata.name}')
echo $POD
kubectl port-forward pod/$POD 8080:8080 >/dev/null &
curl -s localhost:8080/admin/unready
```

After about 10 seconds the watch shows that pod as `0/1 Running`. It is still running and was not restarted. Check the Service:

```bash
kubectl get endpointslices -l kubernetes.io/service-name=web -o jsonpath='{range .items[*].endpoints[*]}{.targetRef.name} ready={.conditions.ready}{"\n"}{end}'
for i in $(seq 1 10); do curl -s localhost:30080/; done | grep -o 'pod: [^ ]*' | sort | uniq -c
```

The pod is listed with `ready=false`, and none of the 10 requests reached it. This is what a pod should do while it cannot serve, for example when a dependency is down: step out of the Service, keep running.

Bring it back:

```bash
curl -s localhost:8080/admin/ready
```

### 3. Make one pod hang

```bash
curl -s localhost:8080/admin/hang
```

Within about 15 seconds the watch shows `RESTARTS 1` for that pod. The liveness probe failed 3 times, so the kubelet killed the container and started a new one. Read what happened:

```bash
kubectl get events --field-selector involvedObject.name=$POD
```

You see `Liveness probe failed: HTTP probe failed with statuscode: 503`, then `Container web failed liveness probe, will be restarted`. The restart also ended your port-forward. Stop it fully and open a new one:

```bash
kill %1
kubectl port-forward pod/$POD 8080:8080 >/dev/null &
```

### 4. Crash a pod

```bash
curl -s localhost:8080/admin/crash
sleep 3
kubectl get pod $POD
kubectl logs $POD --previous | tail -2
```

The restart count goes up again. `--previous` shows the log of the container that died, including `Crashing on purpose with exit code 1`. Kubernetes restarts crashed containers with or without probes; probes are for the cases where the process does not exit.

```bash
kill %1
```

### 5. Repeat yesterday's rollout

In Lab 2 a rollout dropped a few requests. One more setting closes the last gap: when a pod is stopped, it should keep serving for a few seconds while the Service removes it. Add this to the container:

```yaml
          lifecycle:
            preStop:
              sleep:
                seconds: 5     # wait 5 s before the app gets the stop signal
```

```bash
kubectl apply -f deployment.yaml
kubectl rollout status deployment/web
```

Start the loop in the second tab (stop the watch with Ctrl+C first):

```bash
while true; do curl -s --max-time 1 localhost:30080/ || echo "FAILED $(date +%T)"; sleep 0.2; done
```

Roll out v2 in the first tab:

```bash
kubectl set image deployment/web web=ghcr.io/amirilovic/k8s-training-app:v2
kubectl rollout status deployment/web
```

The version changes from v1 to v2 and no `FAILED` line appears. The readiness probe keeps new pods out until they answer, and `preStop` keeps old pods answering until they are out of the Service. Set `image:` in your file to `v2` too.

## Check yourself

```bash
kubectl get pods
kubectl get deployment web -o jsonpath='{.spec.template.spec.containers[0].readinessProbe.httpGet.path} {.spec.template.spec.containers[0].livenessProbe.httpGet.path}'; echo
```

Expected: 3 pods `1/1 Running`, and `/readyz /healthz`.

## Finished early?

- Release the broken version again, now with probes in place, while the curl loop runs:

  ```bash
  kubectl set image deployment/web web=ghcr.io/amirilovic/k8s-training-app:v3-broken
  kubectl get pods
  ```

  This time one new pod crashes and the rollout stops there. The 3 old pods keep serving and the loop shows no failures. Yesterday the same release took the whole app down. Roll back with `kubectl rollout undo deployment/web`.
