#!/usr/bin/env bash
kubectl delete namespace debug-1 debug-2 debug-3 debug-4 debug-5 debug-6 debug-7 --ignore-not-found --wait=false
