# Lab 10 solutions

Each scenario lives in its own namespace and was created by `labs/10-debugging/setup.sh`. Every fix below was tested.

## debug-1: image tag that does not exist

- Symptom: `kubectl get pods` shows `ErrImagePull`, then `ImagePullBackOff`. The pods never start.
- How to find it: `kubectl describe pod` shows an event like `Failed to pull image "ghcr.io/amirilovic/k8s-training-app:v1.0": failed to pull and unpack image ...`. The rest of the message says the registry cannot resolve that reference.
- Cause: the Deployment uses the tag `v1.0`. The image is published as `v1`.
- Fix:

  ```bash
  kubectl -n debug-1 set image deployment/web web=ghcr.io/amirilovic/k8s-training-app:v1
  ```

## debug-2: Service selector does not match the pods

- Symptom: the pods are `Running` and `1/1` ready, but `wget http://web` fails with `Connection refused`.
- How to find it: `kubectl describe service web` shows `Endpoints:` empty, and `Selector: app=webapp`. The pods are labelled `app=web` (`kubectl get pods --show-labels`).
- Cause: a Service sends traffic to pods whose labels match its selector. No pod has `app=webapp`.
- Fix:

  ```bash
  kubectl -n debug-2 patch service web -p '{"spec":{"selector":{"app":"web"}}}'
  ```

## debug-3: Service sends traffic to the wrong port

- Symptom: pods `Running` and ready, the Service has endpoints, but `wget http://web` fails with `Connection refused`.
- How to find it: `kubectl describe service web` shows `TargetPort: 80/TCP`, and the endpoints end in `:80`. The container listens on 8080 (`containerPort: 8080`, and the app log says `Now listening on: http://[::]:8080`).
- Cause: `targetPort: 80`. ASP.NET Core images since .NET 8 listen on 8080, not 80.
- Fix:

  ```bash
  kubectl -n debug-3 patch service web --type json \
    -p '[{"op":"replace","path":"/spec/ports/0/targetPort","value":8080}]'
  ```

## debug-4: Secret key that does not exist

- Symptom: pods stuck in `CreateContainerConfigError`. No logs, because the container never starts.
- How to find it: `kubectl describe pod` shows `Error: couldn't find key ApiKey in Secret debug-4/web-secrets`. `kubectl describe secret web-secrets` shows the Secret has one key, `api-key`.
- Cause: the `secretKeyRef` asks for key `ApiKey`. The Secret calls it `api-key`.
- Fix: change the key in the Deployment to `api-key`, with `kubectl -n debug-4 edit deployment web`, or:

  ```bash
  kubectl -n debug-4 patch deployment web --type json \
    -p '[{"op":"replace","path":"/spec/template/spec/containers/0/env/0/valueFrom/secretKeyRef/key","value":"api-key"}]'
  ```

## debug-5: a bad release

- Symptom: pods in `Error` and `CrashLoopBackOff`, with a growing `RESTARTS` count.
- How to find it: `kubectl logs <pod> --previous` shows `FATAL: ConnectionStrings:Default is not set`. `kubectl get deployment web -o wide` shows the image `v3-broken`. `kubectl rollout history deployment/web` shows two revisions.
- Cause: `setup.sh` deployed `v1`, then released `v3-broken` on top of it. That version refuses to start without a database connection string.
- Fix: roll back to the previous revision:

  ```bash
  kubectl -n debug-5 rollout undo deployment/web
  ```

- Why both pods broke: a rolling update replaces one pod at a time and waits until the new one is available. This Deployment has no readiness probe, so a new pod counts as available the moment its container starts, a fraction of a second before the app exits. The rollout therefore carried on and replaced every working pod. With the readiness probe from Lab 6, the first new pod never becomes ready, the rollout stalls, and the old pods keep serving.

## debug-6: readiness probe on the wrong path (finished early)

- Symptom: pods `Running` but `0/1` ready, `RESTARTS` 0. `wget http://web` fails with `Connection refused`.
- How to find it: `kubectl describe pod` shows `Readiness probe failed: HTTP probe failed with statuscode: 404`, and `Readiness: http-get http://:8080/health`. The Service has no ready endpoints.
- Cause: the probe calls `/health`. The app serves `/readyz` and `/healthz`.
- Fix:

  ```bash
  kubectl -n debug-6 patch deployment web --type json \
    -p '[{"op":"replace","path":"/spec/template/spec/containers/0/readinessProbe/httpGet/path","value":"/readyz"}]'
  ```

## debug-7: CPU request no node can satisfy (finished early)

- Symptom: pods stuck in `Pending`. No node is shown in `kubectl get pods -o wide`.
- How to find it: `kubectl describe pod` shows a `FailedScheduling` event: `0/3 nodes are available: ... Insufficient cpu`.
- Cause: the pod requests `cpu: "64"`, 64 whole CPUs. The scheduler only places a pod on a node with that much unrequested CPU.
- Fix:

  ```bash
  kubectl -n debug-7 set resources deployment/web --requests=cpu=100m
  ```
