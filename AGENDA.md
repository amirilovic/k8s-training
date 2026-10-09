# Agenda

Three days, about a quarter talk and three quarters hands-on. Slides for each day are in [slides/](slides/index.html).

Each day starts with the talks. Then comes a lab block where you work through the labs at your own pace: one block on Monday and Tuesday, two on Wednesday. The times in a block are the latest start for each lab. If you are still on the previous lab by then, apply its solution from `solutions/` and move on. Breaks inside a block are whenever you want.

## Monday 12 October, 09:00 to 13:00

From container to Deployment.

| Time | Block | Type |
|---|---|---|
| 09:00 | Intro, everyone's cluster up |  |
| 09:20 | Docker refresher | talk + demo |
| 09:45 | What Kubernetes is | talk + demo |
| 10:25 | Break |  |
| 10:40 | Labels, ReplicaSets, Deployments | talk |
| 11:00 | Lab block |  |
| 11:00 | [Lab 1: kubectl, Pods, namespaces](labs/01-pods/README.md) | lab |
| 11:50 | [Lab 2: Deployments and rollouts](labs/02-deployments/README.md) | lab |
| 12:45 | Recap and questions |  |

## Tuesday 13 October, 09:00 to 13:00

Services, ingress, config and health.

| Time | Block | Type |
|---|---|---|
| 09:00 | Recap, recreate Monday's Deployment |  |
| 09:15 | Services and DNS | talk |
| 09:35 | Ingress | talk |
| 09:45 | Configuration | talk |
| 10:05 | Probes | talk |
| 10:20 | Break |  |
| 10:35 | Lab block |  |
| 10:35 | [Lab 3: Services](labs/03-services/README.md) | lab |
| 11:15 | [Lab 4: Ingress](labs/04-ingress/README.md) | lab |
| 11:40 | [Lab 5: ConfigMaps and Secrets](labs/05-config/README.md) | lab |
| 12:25 | [Lab 6: Probes](labs/06-probes/README.md) | lab |
| 12:50 | Recap and questions |  |

## Wednesday 14 October, 09:00 to 15:00

Scale, state and shipping.

| Time | Block | Type |
|---|---|---|
| 09:00 | Recap, install metrics-server |  |
| 09:10 | Requests and limits | talk |
| 09:25 | Autoscaling | talk |
| 09:40 | Lab block |  |
| 09:40 | [Lab 7: Limits and HPA](labs/07-limits-hpa/README.md) | lab |
| 10:30 | [Lab 8: Debugging](labs/08-debugging/README.md) | lab |
| 11:30 | Lunch |  |
| 12:00 | Storage and databases | talk |
| 12:20 | Helm | talk |
| 12:30 | Lab block |  |
| 12:30 | [Lab 9: Postgres and a migration Job](labs/09-database/README.md) | lab |
| 13:15 | [Lab 10: Helm](labs/10-helm/README.md) | lab |
| 13:45 | [Capstone in pairs](capstone/README.md) | lab |
| 14:45 | What next, feedback |  |

Before Monday, complete [SETUP.md](SETUP.md) and send the trainer the output of `setup/preflight.sh`.
