# Agenda

Three days, about a quarter talk and three quarters hands-on. Slides for each day are in [slides/](slides/index.html).

Talks come first, then a lab block where you work through the labs at your own pace. Monday has one block and Tuesday two, one before and one after the break. On Wednesday each talk is followed straight away by its lab, and the day ends with a debugging lab. The times in a block are the latest start for each lab. If you are still on the previous lab by then, apply its solution from `solutions/` and move on.

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
| 09:45 | Lab block |  |
| 09:45 | [Lab 3: Services](labs/03-services/README.md) | lab |
| 10:25 | [Lab 4: Ingress](labs/04-ingress/README.md) | lab |
| 10:50 | Break |  |
| 11:05 | Configuration | talk |
| 11:25 | Probes | talk |
| 11:40 | Lab block |  |
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
| 09:40 | [Lab 7: Limits and HPA](labs/07-limits-hpa/README.md) | lab |
| 10:30 | Break |  |
| 10:45 | Jobs and CronJobs | talk |
| 11:00 | [Lab 8: Jobs and CronJobs](labs/08-jobs/README.md) | lab |
| 11:50 | Lunch |  |
| 12:20 | Volumes and databases | talk |
| 12:40 | [Lab 9: Postgres and a migration Job](labs/09-database/README.md) | lab |
| 13:40 | Break |  |
| 13:50 | [Lab 10: Debugging](labs/10-debugging/README.md) | lab |
| 14:40 | What next, feedback |  |

Before Monday, complete [SETUP.md](SETUP.md) and send the trainer the output of `setup/preflight.sh`.
