# Lab 9: a database with persistent storage

Goal: run Postgres in the cluster, keep its data on a PersistentVolumeClaim, apply the EF Core migrations with a Job, and connect the app to it.

Duration: 45 minutes.

## Before you start

You need the state from the end of Lab 7: the `web` Deployment with probes and limits and its HPA, the `web` Service on NodePort 30080, the `web-config` ConfigMap and the `web-secrets` Secret, all in namespace `training`.

If you are behind, catch up with:

```bash
kubectl apply -f ~/k8s-training/solutions/07-limits-hpa/
```

Work in `~/k8s-training/work/` as before.

## Steps

### 1. See what storage the cluster offers

```bash
kubectl get storageclass
```

kind has one StorageClass, `standard`, marked as default. Its provisioner creates a directory on the node for every claim. In a cloud cluster the same request would create a cloud disk instead. Your manifests do not change, only the StorageClass behind them.

`VOLUMEBINDINGMODE` is `WaitForFirstConsumer`. A claim stays `Pending` until a pod that uses it is scheduled, so the disk ends up on the same node as the pod.

### 2. Run Postgres

Create `postgres.yaml` with four objects: a Secret with the database credentials, a PersistentVolumeClaim, a Deployment and a Service.

```yaml
apiVersion: v1
kind: Secret
metadata:
  name: postgres
type: Opaque
stringData:
  POSTGRES_USER: training
  POSTGRES_PASSWORD: training-password
  POSTGRES_DB: training
---
apiVersion: v1
kind: PersistentVolumeClaim
metadata:
  name: postgres-data
spec:
  accessModes: ["ReadWriteOnce"]
  resources:
    requests:
      storage: 1Gi
---
apiVersion: apps/v1
kind: Deployment
metadata:
  name: postgres
spec:
  replicas: 1             # one database, never more: two pods must not share the data directory
  strategy:
    type: Recreate        # stop the old pod before starting the new one, for the same reason
  selector:
    matchLabels:
      app: postgres
  template:
    metadata:
      labels:
        app: postgres
    spec:
      containers:
        - name: postgres
          image: postgres:18-alpine
          ports:
            - containerPort: 5432
          envFrom:
            - secretRef:
                name: postgres
          readinessProbe:
            exec:
              command: ["pg_isready", "-U", "training", "-d", "training"]
            periodSeconds: 5
          resources:
            requests:
              cpu: 100m
              memory: 256Mi
            limits:
              memory: 512Mi
          volumeMounts:
            - name: data
              mountPath: /var/lib/postgresql
      volumes:
        - name: data
          persistentVolumeClaim:
            claimName: postgres-data
---
apiVersion: v1
kind: Service
metadata:
  name: postgres
spec:
  selector:
    app: postgres
  ports:
    - port: 5432
```

A few things to notice:

- `envFrom` turns every key of the Secret into an environment variable. The official Postgres image reads `POSTGRES_USER`, `POSTGRES_PASSWORD` and `POSTGRES_DB` on its first start and creates the user and database.
- `strategy: Recreate` matters here. The default `RollingUpdate` starts the new pod before stopping the old one, and two Postgres processes on one data directory corrupt it.
- Postgres 18 images keep their data under `/var/lib/postgresql/18/docker`, so the volume is mounted at `/var/lib/postgresql`. Older guides mount `/var/lib/postgresql/data`, which does not work with version 18.
- In production your database would be a managed service, and this Deployment would not exist. Everything else in this lab (the Service name, the Secret, the migration Job) stays the same.

Apply it and watch the claim:

```bash
kubectl apply -f postgres.yaml
kubectl get pvc
kubectl rollout status deployment/postgres
kubectl get pvc
```

The claim moves from `Pending` to `Bound` once the Postgres pod is scheduled. `kubectl get pv` shows the volume the provisioner created for it.

### 3. Give the app the connection string

The app reads `ConnectionStrings:Default` from configuration. Add it to the `web-secrets` Secret from Lab 5. Edit `secret.yaml`:

```yaml
apiVersion: v1
kind: Secret
metadata:
  name: web-secrets
type: Opaque
stringData:
  Secrets__ApiKey: training-api-key-1234
  ConnectionStrings__Default: Host=postgres;Database=training;Username=training;Password=training-password
```

`Host=postgres` is the name of the Service you just created. Cluster DNS resolves it, as you saw in Lab 3.

The Secret is already mounted at `/secrets`, so the new key appears there as a file. The app decides at startup whether it has a database, though, so restart the pods:

```bash
kubectl apply -f secret.yaml
kubectl rollout restart deployment/web
kubectl rollout status deployment/web
curl http://localhost:30080/
```

You should see something like:

```
Hello from a ConfigMap | version: v1 | pod: web-7d65844b94-qxt6c | visits: unavailable (42P01: relation "visits" does not exist) Has the migration Job run?
```

The app reaches the database, but the table does not exist yet. Nobody has applied the migrations.

### 4. Apply the migrations with a Job

A Job runs a pod until it finishes successfully, then stops. That fits a database migration: it should run once per release, not continuously.

The app image has a `migrate` command that applies the EF Core migrations and exits. Create `migrate-job.yaml`:

```yaml
apiVersion: batch/v1
kind: Job
metadata:
  name: migrate
spec:
  backoffLimit: 3
  template:
    spec:
      restartPolicy: Never
      containers:
        - name: migrate
          image: ghcr.io/amirilovic/k8s-training-app:v1
          args: ["migrate"]     # same image as the app, different command
          env:
            - name: ConnectionStrings__Default
              valueFrom:
                secretKeyRef:
                  name: web-secrets
                  key: ConnectionStrings__Default
```

```bash
kubectl apply -f migrate-job.yaml
kubectl wait --for=condition=complete job/migrate --timeout=120s
kubectl get jobs
kubectl logs job/migrate
```

The log ends with `migrate: applied 1 migration(s): 20261012000000_CreateVisits`. `kubectl get pods` shows the Job's pod as `Completed`. It stays there so you can read its logs.

### 5. Use the app

```bash
for i in 1 2 3 4 5; do curl -s http://localhost:30080/; done
```

The visit counter goes up with every request, whichever pod answers. All `web` pods share one database.

### 6. Delete the database pod

```bash
kubectl delete pod -l app=postgres
kubectl rollout status deployment/postgres
curl http://localhost:30080/
```

The Deployment creates a new Postgres pod. It mounts the same claim, so the counter continues where it stopped. While Postgres is down, the app's readiness check fails (it includes a database check) and the Service stops sending it traffic. Liveness does not include the database, so Kubernetes does not restart the app pods.

## Check yourself

```bash
kubectl get pvc postgres-data          # STATUS Bound
kubectl get job migrate                # COMPLETIONS 1/1
curl -s http://localhost:30080/        # ends with: visits: <a number>
```

## Finished early?

- Show what the claim protects you from. Copy `postgres.yaml`, replace the `persistentVolumeClaim` volume with `emptyDir: {}`, apply it, run the migration Job again and make a few requests. Then delete the Postgres pod. The table is gone, and the app asks for the migration again. Switch back to the claim afterwards (the old data is still in it).
- Run the migration Job again. `kubectl apply -f migrate-job.yaml` says `unchanged`, because a finished Job is never run twice. Delete it first: `kubectl delete job migrate`, then apply. The log now says `applied 0 migration(s): none pending`.
- Write a CronJob that prints the number of visits every minute. Use the `postgres:18-alpine` image, the command `psql -h postgres -U training -d training -c "select count(*) from visits"`, and the environment variable `PGPASSWORD` from the `postgres` Secret (key `POSTGRES_PASSWORD`). Use `schedule: "* * * * *"` and read the output with `kubectl logs` on the pods it creates.
- Look at the volume on the node: `kubectl get pv`, then `docker exec -it training-worker ls /var/local-path-provisioner` (or `training-worker2`, wherever the pod runs).
