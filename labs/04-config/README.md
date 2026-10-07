# Lab 4: ConfigMaps and Secrets

The same image runs in dev and in production with different settings. You give the app its configuration three ways: an environment variable, an `appsettings.json` file from a ConfigMap, and an API key from a Secret. You see how this maps onto .NET configuration, and what happens when you change a ConfigMap while the app runs. About 45 minutes.

## Before you start

- Deployment `web` and the NodePort Service from Lab 3 run, and `curl -s localhost:30080/` answers.
- Set the image in your `deployment.yaml` back to `v1`, so it matches the steps below.
- Work in `~/k8s-training/work`.

Catch-up: `kubectl apply -f ~/k8s-training/solutions/03-services/`

How the app reads configuration: like any ASP.NET Core app it reads `appsettings.json`, then a file at `/config/appsettings.json` if one is mounted, then files in `/secrets`, then environment variables. A later source wins. The greeting on the start page is the setting `Greeting:Text`.

## Steps

### 1. Set the greeting with an environment variable

In .NET configuration, `__` (two underscores) in a variable name stands for `:`. So `Greeting__Text` sets `Greeting:Text`. Add `env` to the container in `deployment.yaml`:

```yaml
      containers:
        - name: web
          image: ghcr.io/amirilovic/k8s-training-app:v1
          ports:
            - containerPort: 8080
          env:
            - name: Greeting__Text
              value: Hello from an env var
```

```bash
kubectl apply -f deployment.yaml
kubectl rollout status deployment/web
curl -s localhost:30080/
```

Changing the pod template started a rollout: environment variables are fixed when a container starts, so new pods are needed.

### 2. Put appsettings.json in a ConfigMap

Create `appsettings.json` in your work folder:

```json
{
  "Greeting": {
    "Text": "Hello from a ConfigMap"
  }
}
```

Turn it into a ConfigMap file and apply it:

```bash
kubectl create configmap web-config --from-file=appsettings.json --dry-run=client -o yaml > configmap.yaml
cat configmap.yaml
kubectl apply -f configmap.yaml
```

The ConfigMap has one key, `appsettings.json`, whose value is the whole file.

### 3. Mount it into the pod

Add a volume to the pod and mount it at `/config`. In `deployment.yaml`, the container gets `volumeMounts` and the pod spec gets `volumes`:

```yaml
    spec:
      containers:
        - name: web
          image: ghcr.io/amirilovic/k8s-training-app:v1
          ports:
            - containerPort: 8080
          env:
            - name: Greeting__Text
              value: Hello from an env var
          volumeMounts:
            - name: config            # refers to the volume below
              mountPath: /config      # each key of the ConfigMap becomes a file here
              readOnly: true
      volumes:
        - name: config
          configMap:
            name: web-config
```

```bash
kubectl apply -f deployment.yaml
kubectl rollout status deployment/web
kubectl exec deploy/web -- cat /config/appsettings.json
curl -s localhost:30080/
```

The file is there, but the greeting still says "Hello from an env var". Environment variables come later in the provider order, so they win. Remove the `env` block from `deployment.yaml`, apply, and curl again: now you see "Hello from a ConfigMap".

### 4. Put the API key in a Secret

```bash
kubectl create secret generic web-secrets --from-literal=Secrets__ApiKey=training-api-key-1234 --dry-run=client -o yaml > secret.yaml
cat secret.yaml
kubectl apply -f secret.yaml
```

The value in `secret.yaml` looks scrambled. It is only base64:

```bash
kubectl get secret web-secrets -o jsonpath='{.data.Secrets__ApiKey}' | base64 -d; echo
```

Anyone who can read the Secret can read the key. Secrets are kept apart from ConfigMaps so that access to them can be limited, not because they are encrypted. Never commit a real `secret.yaml` to git.

### 5. Mount the Secret

Add a second mount and volume to `deployment.yaml`:

```yaml
          volumeMounts:
            - name: config
              mountPath: /config
              readOnly: true
            - name: secrets
              mountPath: /secrets
              readOnly: true
      volumes:
        - name: config
          configMap:
            name: web-config
        - name: secrets
          secret:
            secretName: web-secrets
```

```bash
kubectl apply -f deployment.yaml
kubectl rollout status deployment/web
kubectl exec deploy/web -- ls /secrets
curl -s localhost:30080/api/info; echo
```

`/secrets` has one file, `Secrets__ApiKey`. The app uses ASP.NET Core's key-per-file provider, which reads each file name as a setting name and its content as the value. `/api/info` shows `"apiKey":"****1234"`.

### 6. Change the ConfigMap while the app runs

Edit the text in `configmap.yaml` to `Hello from an edited ConfigMap` and apply only the ConfigMap:

```bash
kubectl apply -f configmap.yaml
```

Check the file inside a pod every few seconds until it changes. That takes up to a minute:

```bash
kubectl exec deploy/web -- cat /config/appsettings.json
```

Now ask the app:

```bash
curl -s localhost:30080/
```

The file changed, but the app still shows the old greeting, even though it loads the file with `reloadOnChange: true`. Kubernetes updates a mounted ConfigMap by swapping a symbolic link, and .NET's file watcher does not notice that. Environment variables never update in a running container at all.

The reliable way to apply new configuration is to restart the pods:

```bash
kubectl rollout restart deployment/web
kubectl rollout status deployment/web
curl -s localhost:30080/
```

## Check yourself

```bash
curl -s localhost:30080/
curl -s localhost:30080/api/info; echo
kubectl get configmap/web-config secret/web-secrets
```

Expected: the greeting is the one from your ConfigMap, `/api/info` shows `"apiKey":"****1234"`, and both objects exist.

## Finished early?

- Make the app notice ConfigMap changes without a restart. Add this environment variable to the container, apply, then edit and apply the ConfigMap again:

  ```yaml
          env:
            - name: DOTNET_USE_POLLING_FILE_WATCHER
              value: "true"
  ```

  .NET now checks the file every few seconds instead of waiting for a change event. The new greeting shows up shortly after the file changes in the pod.

- Read a single Secret value as an environment variable instead of a file:

  ```yaml
          env:
            - name: Secrets__ApiKey
              valueFrom:
                secretKeyRef:
                  name: web-secrets
                  key: Secrets__ApiKey
  ```

  Then replace `valueFrom` with `value: env-key-9999` and apply. The file in `/secrets` still says `...1234`. Which one does `/api/info` show, and why?

- Look at how the mounted ConfigMap is built: `kubectl exec deploy/web -- ls -la /config`. The file is a link to `..data/appsettings.json`, and `..data` is a link to a timestamped folder. That swap of links is what .NET's watcher misses.
