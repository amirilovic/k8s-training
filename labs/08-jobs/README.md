# Lab 8: Jobs and CronJobs

Goal: run work that finishes instead of running forever: a one-off Job, a Job that fails and gives up, a Job that runs many times in parallel, and a CronJob that starts a Job on a schedule.

Duration: 50 minutes.

## Before you start

- Your namespace is `training`. Nothing from earlier labs is needed.
- Work in `~/k8s-training/work`.
- Keep a second Ubuntu tab open for watching.

A Deployment keeps its pods running: when a pod exits, it starts a new one. A Job is the opposite. It runs a pod until the pod finishes successfully, then stops. Use it for anything that should end: a database migration, a report, a data import, a cleanup.

## Steps

### 1. Generate a Job

Use a dry run, as in Labs 1 and 2:

```bash
kubectl create job hello --image=busybox:1.37 --dry-run=client -o yaml -- sh -c 'echo "Hello from a Job on $(hostname)"; sleep 5' > job.yaml
```

Remove the top-level `creationTimestamp`, the `metadata:` block inside `template` (it only holds another `creationTimestamp`), `resources: {}` and `status: {}`, so the file matches this:

```yaml
apiVersion: batch/v1
kind: Job
metadata:
  name: hello
spec:
  template:                   # the pod to run, as in a Deployment
    spec:
      containers:
        - command:
            - sh
            - -c
            - echo "Hello from a Job on $(hostname)"; sleep 5
          image: busybox:1.37
          name: hello
      restartPolicy: Never    # a Job's pod must not restart forever: Never or OnFailure
```

### 2. Run it

In the second tab:

```bash
kubectl get pods -w
```

In the first tab:

```bash
kubectl apply -f job.yaml
kubectl wait --for=condition=complete job/hello --timeout=120s
kubectl get job hello
kubectl logs job/hello
```

The watch shows the pod `Running` for 5 seconds, then `Completed`. `kubectl get job` shows `STATUS Complete` and `COMPLETIONS 1/1`. The log says `Hello from a Job on hello-xxxxx`.

The finished pod stays in `kubectl get pods` as `Completed`, so you can still read its log. Nothing restarts it.

Apply the same file again: `job.batch/hello unchanged`. A finished Job never runs twice. To run it again, delete it first: `kubectl delete job hello`, then apply.

### 3. A Job that fails

Create `fail-job.yaml`:

```yaml
apiVersion: batch/v1
kind: Job
metadata:
  name: fail
spec:
  backoffLimit: 2             # retry twice, then give up (the default is 6)
  template:
    spec:
      containers:
        - name: fail
          image: busybox:1.37
          command: ["sh", "-c", "echo 'Trying...'; exit 1"]
      restartPolicy: Never
```

```bash
kubectl apply -f fail-job.yaml
kubectl wait --for=condition=failed job/fail --timeout=180s
kubectl get job fail
kubectl get pods -l job-name=fail
kubectl describe job fail
```

After about 30 seconds the Job is `Failed`, with three pods in `Error`: the first try and two retries. The waits between tries grow, about 10 s, then 20 s. The events at the bottom of `describe` end with `BackoffLimitExceeded: Job has reached the specified backoff limit`.

With `restartPolicy: Never`, every try is a new pod, and the failed ones stay for you to read: `kubectl logs fail-xxxxx`. With `OnFailure`, the same pod restarts its container instead.

### 4. Many runs, a few at a time

A Job can run its pod several times. Create `batch-job.yaml`:

```yaml
apiVersion: batch/v1
kind: Job
metadata:
  name: batch
spec:
  completions: 6              # the Job is done after 6 successful pods
  parallelism: 2              # at most 2 run at the same time
  template:
    spec:
      containers:
        - name: batch
          image: busybox:1.37
          command: ["sh", "-c", "echo \"Processing on $(hostname)\"; sleep 5"]
      restartPolicy: Never
```

```bash
kubectl apply -f batch-job.yaml
kubectl get job batch -w
```

`COMPLETIONS` counts up `0/6`, `2/6`, `4/6`, `6/6`, two pods at a time, in about 25 seconds. Stop the watch with Ctrl+C. This is how you split a big batch over several pods.

### 5. A CronJob

A CronJob creates a Job on a schedule. Generate one that runs every minute:

```bash
kubectl create cronjob tick --image=busybox:1.37 --schedule='* * * * *' --dry-run=client -o yaml -- sh -c 'date; echo tick' > cronjob.yaml
```

Remove the top-level `creationTimestamp`, the `metadata:` blocks inside `jobTemplate` and inside `template` (they only hold `creationTimestamp` and a `name`), `resources: {}` and `status: {}`, so it matches this:

```yaml
apiVersion: batch/v1
kind: CronJob
metadata:
  name: tick
spec:
  schedule: "* * * * *"       # minute hour day-of-month month day-of-week
  jobTemplate:                # the Job to create each time
    spec:
      template:
        spec:
          containers:
            - command:
                - sh
                - -c
                - date; echo tick
              image: busybox:1.37
              name: tick
          restartPolicy: OnFailure
```

```bash
kubectl apply -f cronjob.yaml
kubectl get cronjob tick
kubectl get jobs -w
```

At the start of each minute a new Job appears, named `tick-` plus a number. Stop the watch after two of them with Ctrl+C, then look at one:

```bash
kubectl get cronjob tick
kubectl logs job/tick-xxxxxxxx
```

`LAST SCHEDULE` shows when it last ran. The log prints the date in UTC: schedules use the cluster's time zone, which is UTC unless you set `spec.timeZone`, for example `timeZone: Europe/Berlin`.

### 6. Run it now, then pause it

Start a run without waiting for the schedule:

```bash
kubectl create job tick-now --from=cronjob/tick
kubectl logs job/tick-now
```

This is handy for testing a CronJob, or for running a nightly job once more by hand.

To pause the schedule, add `suspend: true` under `spec:` in `cronjob.yaml` and apply:

```yaml
spec:
  schedule: "* * * * *"
  suspend: true
```

```bash
kubectl apply -f cronjob.yaml
kubectl get cronjob tick
```

`SUSPEND` is now `True` and no new Jobs appear. The CronJob keeps only the last 3 successful Jobs and the last failed one (`successfulJobsHistoryLimit` and `failedJobsHistoryLimit`), so old runs clean themselves up.

## Check yourself

```bash
kubectl get jobs hello fail batch    # Complete 1/1, Failed 0/1, Complete 6/6
kubectl get cronjob tick             # SUSPEND True, LAST SCHEDULE set
```

## Finished early?

- Clean up finished Jobs automatically. Add `ttlSecondsAfterFinished: 60` under the Job's `spec:` in `job.yaml`, delete and re-apply it, and watch the Job and its pod disappear a minute after they finish.
- Stop a Job that runs too long. Change the command in `job.yaml` to `sleep 300` and add `activeDeadlineSeconds: 20` under `spec:`. After 20 seconds the Job fails with `DeadlineExceeded`, however many retries are left.
- Decide what happens when a run is still going at the next schedule. Make the CronJob's command `sleep 90`, unsuspend it, and compare `concurrencyPolicy: Allow` (the default, runs overlap), `Forbid` (the new run is skipped) and `Replace` (the old run is stopped).
- Call your app on a schedule. If the `web` Service from earlier labs still runs, write a CronJob with the command `wget -qO- http://web/` and read its output with `kubectl logs`. This is how health reports or cache warm-ups often run.
- Clean up: `kubectl delete job hello fail batch tick-now`, `kubectl delete cronjob tick`. Deleting a CronJob also deletes the Jobs it created.
