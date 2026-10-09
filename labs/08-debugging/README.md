# Lab 8: debugging

Goal: find and fix five broken deployments on your own, using only `kubectl get`, `describe`, `logs` and events. For each one, write down the symptom you saw first, the cause, and the fix.

Duration: 40 minutes.

## Before you start

You need a running cluster. Nothing from earlier labs is needed, and nothing in namespace `training` is touched.

Create the broken scenarios:

```bash
~/k8s-training/labs/08-debugging/setup.sh
```

This creates seven namespaces, `debug-1` to `debug-7`. Each one contains a Deployment called `web` and a Service called `web`, and each one is broken in a different way. Scenarios 1 to 5 are the main exercise. Scenarios 6 and 7 are for when you are done.

Do not read the YAML files in `broken/` first. In real life you also start from a symptom, not from the manifest.

## How to investigate

Work through each namespace with the same routine. Add `-n debug-1` (or the namespace you are in) to every command, or switch to it:

```bash
kubectl config set-context --current --namespace debug-1
```

Switch back to `training` when you are done.

1. Look at the pods. What does `STATUS` say, how many are `READY`, how many `RESTARTS`?

   ```bash
   kubectl get pods
   ```

2. Read the events of a pod that looks wrong. The `Events` section at the bottom of `describe` usually names the problem.

   ```bash
   kubectl describe pod <pod-name>
   ```

3. Read the logs. If the container keeps restarting, the interesting log is from the previous attempt.

   ```bash
   kubectl logs <pod-name>
   kubectl logs <pod-name> --previous
   ```

4. If the pods look healthy, check the Service. Does it have endpoints, and do they point at the right port?

   ```bash
   kubectl describe service web
   kubectl get endpointslices -l kubernetes.io/service-name=web
   ```

5. Test the Service from inside the namespace, the way another app would call it:

   ```bash
   kubectl run test --rm -it --restart=Never --image=busybox:1.37 -- wget -qO- -T 3 http://web
   ```

   When it works, you see `Hello from Kubernetes | version: v1 | ...`.

6. Recent events for the whole namespace, oldest first:

   ```bash
   kubectl get events --sort-by=.lastTimestamp
   ```

## Steps

For each of `debug-1` to `debug-5`:

1. Find the symptom with the routine above. Write it down in one line, for example "pods stuck in X" or "pods run, but wget gets Y".
2. Find the cause. Name the field in the manifest that is wrong.
3. Fix it. Either edit the live object with `kubectl edit deployment web` or `kubectl edit service web`, or use a targeted command (`kubectl set image`, `kubectl rollout undo`, and so on). One scenario has a one-command fix that does not involve editing YAML at all.
4. Confirm the fix with the `wget` test from step 5 of the routine.

At the end, we go through the five scenarios together. Be ready to say what the first symptom was, because that is what you will see at work.

## Check yourself

Each of these prints `Hello from Kubernetes | version: v1 | ...`:

```bash
for n in 1 2 3 4 5; do
  kubectl -n debug-$n run test --rm -i --restart=Never --image=busybox:1.37 -- wget -qO- -T 3 http://web 2>/dev/null | head -1
done
```

## Finished early?

- Fix `debug-6` and `debug-7`. They are broken in ways that look different from the first five.
- Scenario 5 broke both pods, even though Kubernetes replaces pods one at a time during a rollout. Find out why. Hint: compare the Deployment with yours from Lab 6, and think about when Kubernetes considers a new pod "available".
- Install k9s if you have not yet (it is in the setup guide), run `k9s`, type `:pods` and press `0` to show all namespaces. Find the broken pods, press `d` to describe and `l` to see logs.

## Clean up

```bash
~/k8s-training/labs/08-debugging/cleanup.sh
kubectl config set-context --current --namespace training
```
