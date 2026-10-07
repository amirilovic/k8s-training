# Capstone

Goal: in pairs, deploy the app and its database to a fresh namespace with Helm, configured the way you would run it for real. You are done when the check script passes.

Duration: 60 minutes.

## Before you start

- Work in pairs, on one laptop. Swap who types every 15 minutes.
- Pick a pair name, for example your first names: `anna-ben`. You use it in the namespace and the hostname, so it must be lowercase letters, digits and dashes.
- Traefik from Lab 9 must be running: `kubectl get pods -n traefik`.
- metrics-server must be running: `kubectl top nodes` prints numbers.

## The task

Deploy to namespace `capstone-<pair>` (for example `capstone-anna-ben`), with Helm, a release that has:

- the training app, `ghcr.io/amirilovic/k8s-training-app:v1`, with at least 2 ready pods
- its greeting from a ConfigMap
- its API key and database connection string from a Secret
- a readiness probe on `/readyz` and a liveness probe on `/healthz`
- CPU and memory requests, and a memory limit
- a HorizontalPodAutoscaler on CPU, minimum 2 pods
- Postgres with its data on a PersistentVolumeClaim
- the migrations applied, so the visit counter works
- an Ingress at `http://capstone-<pair>.localhost:30081`

## How to start

You have two options:

1. Extend your own chart from Lab 9. Add templates for the HPA, the Ingress, Postgres (from Lab 6) and the migration Job. This is the better exercise.
2. Start from the reference chart in `~/k8s-training/solutions/09-helm/web/`. Read every template first and be ready to explain what each one does. Then write a values file for your pair, for example `values-capstone.yaml`, that turns on autoscaling and sets your Ingress host.

Either way, render before you install:

```bash
helm template web ./web -f values-capstone.yaml | less
```

and install into your namespace:

```bash
helm install web ./web -n capstone-<pair> --create-namespace -f values-capstone.yaml --wait --timeout 3m
```

If `--wait` times out, do not reinstall. Find out why, with the routine from Lab 7: `kubectl get pods -n capstone-<pair>`, then `describe` and `logs`. Fix the chart and run `helm upgrade` with the same arguments.

Things that commonly go wrong:

- The connection string's `Host=` must be the name of your Postgres Service.
- The migrations must run after Postgres is up. A Job retries on its own (the app's `migrate` command also waits up to a minute for the database), or use a Helm hook as the reference chart does.
- The app reads the connection string at startup. If you add it later, restart the Deployment.
- An HPA needs CPU requests on the container.
- The Ingress needs `ingressClassName: traefik` and your exact host.

## Check

Run the check script from your copy of the repo:

```bash
~/k8s-training/capstone/check.sh capstone-<pair> capstone-<pair>.localhost
```

It checks every point in the task list, prints `PASS` or `FAIL` with a hint for each, and finishes with the totals. You are done when it reports `0 failed`. Then open `http://capstone-<pair>.localhost:30081` in the browser and show the trainer.

## Finished early?

Pick one.

### Drain a node without downtime

1. Spread your pods: add a `topologySpreadConstraints` entry with `topologyKey: kubernetes.io/hostname` to the Deployment, so the pods land on different nodes.
2. Add a PodDisruptionBudget with `minAvailable: 1` and a selector for your app's pods.
3. In a second terminal, run a request loop against your Ingress:

   ```bash
   while true; do curl -s -o /dev/null -w "%{http_code}\n" http://capstone-<pair>.localhost:30081/; sleep 0.2; done
   ```

4. Drain one worker, then bring it back:

   ```bash
   kubectl get pods -n capstone-<pair> -o wide          # which nodes are your pods on?
   kubectl drain training-worker --ignore-daemonsets --delete-emptydir-data
   kubectl get pods -n capstone-<pair> -o wide
   kubectl uncordon training-worker
   ```

   Watch the loop. The app pods move without errors. What happens if Postgres was on that node, and why?

### Least-privilege access with RBAC

1. Create a ServiceAccount `reader` in your namespace.
2. Create a Role that allows `get`, `list` and `watch` on `pods` and `pods/log`, and a RoleBinding that gives it to `reader`.
3. Test it without logging in as that account:

   ```bash
   kubectl auth can-i list pods -n capstone-<pair> --as system:serviceaccount:capstone-<pair>:reader      # yes
   kubectl auth can-i delete pods -n capstone-<pair> --as system:serviceaccount:capstone-<pair>:reader    # no
   kubectl auth can-i list pods -n training --as system:serviceaccount:capstone-<pair>:reader             # no
   ```

4. Run a command as that account: `kubectl get pods -n capstone-<pair> --as system:serviceaccount:capstone-<pair>:reader`. Then try `kubectl get secrets` the same way.

## Clean up

When the trainer has seen it:

```bash
helm uninstall web -n capstone-<pair>
kubectl delete namespace capstone-<pair>
```

`helm uninstall` also deletes the PersistentVolumeClaim the chart created, so the database is gone after this.
