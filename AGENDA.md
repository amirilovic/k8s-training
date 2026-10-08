# Agenda

Three days, about a quarter talk and three quarters hands-on. Slides for each day are in [slides/](slides/index.html).

## Monday 12 October, 09:00 to 13:00

From container to Deployment.

| Time | Block | Type |
|---|---|---|
| 09:00 | Intro, everyone's cluster up |  |
| 09:20 | Docker refresher | talk + demo |
| 09:45 | What Kubernetes is | talk + demo |
| 10:25 | Break |  |
| 10:40 | [Lab 1: kubectl, Pods, namespaces](labs/01-pods/README.md) | lab |
| 11:30 | Labels, ReplicaSets, Deployments | talk |
| 11:50 | [Lab 2: Deployments and rollouts](labs/02-deployments/README.md) | lab |
| 12:45 | Recap and questions |  |

## Tuesday 13 October, 09:00 to 13:00

Services, config and health.

| Time | Block | Type |
|---|---|---|
| 09:00 | Recap, recreate Monday's Deployment |  |
| 09:20 | Services and DNS | talk |
| 09:40 | [Lab 3: Services](labs/03-services/README.md) | lab |
| 10:20 | Break |  |
| 10:35 | Configuration | talk |
| 10:55 | [Lab 4: ConfigMaps and Secrets](labs/04-config/README.md) | lab |
| 11:40 | Break |  |
| 11:50 | Probes and resources | talk |
| 12:10 | [Lab 5: Probes and limits](labs/05-probes-limits/README.md) | lab |
| 12:50 | Recap and questions |  |

## Wednesday 14 October, 09:00 to 15:00

State, scale and shipping.

| Time | Block | Type |
|---|---|---|
| 09:00 | Recap, install metrics-server |  |
| 09:10 | Storage and databases | talk |
| 09:30 | [Lab 6: Postgres and a migration Job](labs/06-database/README.md) | lab |
| 10:15 | Break |  |
| 10:30 | [Lab 7: Debugging](labs/07-debugging/README.md) | lab |
| 11:10 | Autoscaling | talk |
| 11:25 | [Lab 8: HPA](labs/08-hpa/README.md) | lab |
| 11:55 | Lunch |  |
| 12:25 | Helm | talk |
| 12:35 | [Lab 9: Helm](labs/09-helm/README.md) | lab |
| 13:05 | Ingress | talk |
| 13:15 | [Lab 10: Ingress](labs/10-ingress/README.md) | lab |
| 13:40 | [Capstone in pairs](capstone/README.md) | lab |
| 14:40 | What next, feedback |  |

Before Monday, complete [SETUP.md](SETUP.md) and send the trainer the output of `setup/preflight.sh`.
