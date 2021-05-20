@echo off
REM destroys all K8s services in "kpi-collector" namespace

kubectl delete ns kpi-collector