# Lab 4: Ingress

Goal: route browser traffic by hostname through the ingress controller to two different Services, and learn to read the Ingress files you will meet in real projects.

Duration: 25 minutes.

## Before you start

- The `web` Deployment and Service from Lab 3 run in namespace `training`. The Service listens on port 80 and forwards to the container's 8080.
- `web` runs `v1`. Lab 2 ended on `v2`, so set the image in your `deployment.yaml` back to `v1` and apply it. Otherwise both hostnames below show the same version.
- Work in `~/k8s-training/work`.

Catch-up: `kubectl apply -f ~/k8s-training/solutions/03-services/`

## How the traffic flows

```
browser: http://app.localhost:30081
  -> localhost:30081 on your machine   (kind publishes NodePort 30081)
  -> Traefik's Service, NodePort 30081
  -> Traefik pod: reads the Host header "app.localhost", finds the matching Ingress rule
  -> Service web, port 80
  -> one of the web pods, port 8080
```

Chrome and Edge resolve every `*.localhost` name to your own machine, and so does `curl` in Ubuntu. You do not need to edit any hosts file.

## Steps

### 1. Install the ingress controller

An Ingress is only a set of routing rules. It does nothing until an ingress controller reads it and routes the traffic. Your cluster has none yet. Install Traefik:

```bash
kubectl apply -f ~/k8s-training/setup/traefik.yaml
kubectl rollout status deployment/traefik -n traefik
kubectl get pods,svc -n traefik
kubectl get ingressclass
```

You see one Traefik pod, its Service of type NodePort on port 30081, and an IngressClass called `traefik`. Open `http://test.localhost:30081` in the browser: `404 page not found` comes from Traefik. It is running, but no Ingress tells it where to send traffic yet.

`traefik.yaml` is about 270 lines of ordinary Kubernetes objects: a Namespace, a ServiceAccount and RBAC rules, a Deployment, a Service and the IngressClass. Nobody wrote it by hand. It was generated from Traefik's Helm chart, and on Wednesday you will see how.

### 2. Deploy a second version

Create `web-v2.yaml`:

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: web-v2
spec:
  replicas: 1
  selector:
    matchLabels:
      app: web-v2
  template:
    metadata:
      labels:
        app: web-v2
    spec:
      containers:
        - name: web
          image: ghcr.io/amirilovic/k8s-training-app:v2
          ports:
            - containerPort: 8080
          readinessProbe:      # send traffic only once the app answers; Lab 6 explains probes
            httpGet:
              path: /readyz
              port: 8080
---
apiVersion: v1
kind: Service
metadata:
  name: web-v2
spec:
  selector:
    app: web-v2
  ports:
    - port: 80
      targetPort: 8080
```

```bash
kubectl apply -f web-v2.yaml
kubectl rollout status deployment/web-v2
```

### 3. Write the Ingress

Create `ingress.yaml`:

```yaml
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: web
spec:
  ingressClassName: traefik    # which controller handles this Ingress
  rules:
    - host: app.localhost
      http:
        paths:
          - path: /
            pathType: Prefix
            backend:
              service:
                name: web
                port:
                  number: 80
    - host: v2.localhost
      http:
        paths:
          - path: /
            pathType: Prefix
            backend:
              service:
                name: web-v2
                port:
                  number: 80
```

```bash
kubectl apply -f ingress.yaml
kubectl get ingress web
kubectl describe ingress web
```

`describe` lists each host and path with the Service and pod addresses behind it.

### 4. Open it

In the Windows browser, open:

- `http://app.localhost:30081` shows `version: v1`.
- `http://v2.localhost:30081` shows `version: v2`.
- `http://other.localhost:30081` shows `404 page not found`. Traefik answered, but no rule matches that host.

The same from Ubuntu:

```bash
curl http://app.localhost:30081/
curl http://v2.localhost:30081/
curl -i http://other.localhost:30081/
```

All three hostnames reach the same Traefik pod on the same port. Only the `Host` header differs, and the Ingress rules route on it. You can prove that by sending the header yourself:

```bash
curl -H "Host: v2.localhost" http://localhost:30081/
```

### 5. Two ways to pick the controller

A cluster can run several ingress controllers. Each Ingress says which one should handle it. The current way is the field you used:

```yaml
spec:
  ingressClassName: traefik
```

Older manifests use an annotation instead. Most Ingresses in Epic's clusters still do:

```yaml
metadata:
  annotations:
    kubernetes.io/ingress.class: alb
```

Both still work. Kubernetes marked the annotation as deprecated years ago, and `kubectl apply` prints a warning when it sees it:

```
Warning: annotation "kubernetes.io/ingress.class" is deprecated, please use 'spec.ingressClassName' instead
```

Write new Ingresses with `ingressClassName`. Recognise the annotation when you read existing charts.

Note: Traefik also serves an Ingress that names no class at all. In a cluster with more than one controller, that Ingress could be picked up by any of them, or by none, so always set the class.

### 6. Read a cloud Ingress

On a managed cluster, the ingress controller is usually the cloud's load balancer. On AWS, the AWS Load Balancer Controller creates an Application Load Balancer (ALB) for an Ingress. Everything the Ingress spec cannot express goes into controller-specific annotations. Here is a typical example, with made-up values:

```yaml
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: web
  annotations:
    kubernetes.io/ingress.class: alb
    alb.ingress.kubernetes.io/scheme: internal                     # not reachable from the internet
    alb.ingress.kubernetes.io/target-type: ip                      # send traffic straight to pod IPs
    alb.ingress.kubernetes.io/listen-ports: '[{"HTTPS": 443}]'
    alb.ingress.kubernetes.io/certificate-arn: arn:aws:acm:us-east-1:000000000000:certificate/example
    alb.ingress.kubernetes.io/ssl-redirect: "443"
    alb.ingress.kubernetes.io/healthcheck-path: /readyz
    alb.ingress.kubernetes.io/group.name: example-internal         # share one ALB between several Ingresses
spec:
  rules:
    - host: web.example.internal
      http:
        paths:
          - path: /
            pathType: Prefix
            backend:
              service:
                name: web
                port:
                  number: 80
```

The `spec` part is the same as yours. The annotations configure the load balancer: TLS certificate, health check, internal or public, and which ALB to share. They only mean something to the AWS controller. Traefik ignores them, and Traefik has its own annotations (`traefik.ingress.kubernetes.io/*`). That is why Ingress files look so different between companies, and why a change of controller means rewriting annotations.

## Check yourself

```bash
kubectl get ingress web                     # CLASS traefik, HOSTS app.localhost,v2.localhost
curl -s http://app.localhost:30081/         # version: v1
curl -s http://v2.localhost:30081/          # version: v2
curl -s -o /dev/null -w "%{http_code}\n" http://other.localhost:30081/   # 404
```

## Finished early?

- Add a path rule: send `app.localhost/v2` to `web-v2`. The request reaches the app as `/v2`, and the app has no such page, so you get a 404 from the app, not from Traefik. Stripping the prefix needs a Traefik middleware, configured through a Traefik-specific annotation. This is exactly the kind of thing that does not carry over between controllers.
- Think about what the app sees behind a proxy. ASP.NET Core gets the request from Traefik, so `HttpContext.Request` has Traefik's address and the scheme `http`, even when the browser used HTTPS on a real load balancer. Redirects and generated links then point to the wrong place. Setting `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` on the Deployment makes ASP.NET Core read the `X-Forwarded-*` headers the proxy adds. The training app builds no links, so you will not see a difference here, but remember the variable for your own apps.
- Open `http://app.localhost:30081` and `http://localhost:30080` side by side. Both reach the same pods, once through Traefik and once through the NodePort from Lab 3. In real clusters apps use ClusterIP Services and all outside traffic comes in through the ingress controller. Keep the NodePort for now: the next labs still use `localhost:30080`.
