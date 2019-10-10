## Kubernetes Deploy for DroneCI

A simple way to deploy web or tcp application with drone ci.

> You can use it with other ci yet~~


### Enviroment Description

Name    |DefaultValue     |     Description
 -------- | :-----------:  | :-----------: 
PLUGIN_NAMESPACE   | DRONE_REPO_NAMESPACE | k8s namespace, if not exists, will create
PLUGIN_NAME        | DRONE_REPO without DRONE_REPO_NAMESPACE | deployname attr  
PLUGIN_ENVIRONMENT  |-     | deployname attr  
PLUGIN_IMAGE       |-    | the deploy image  
PLUGIN_CPU         |500m     | k8s limit cpu  
PLUGIN_MEM         |1024Mi     | k8s limit memory 
PLUGIN_RSVP        |true     | if set true,will add resource request to deployment with limit cpu/memory  
PLUGIN_PORT        |0    | deployment port , if set to <=0, will not create service  
PLUGIN_SERVICE_TYPE|ClusterIP     | service type, supported values: "ClusterIP", "ExternalName", "LoadBalancer", "NodePort"
PLUGIN_URL         |-    | if set , will create ingress with the url, not include http/https e.g. test.simcu.com
PLUGIN_ACME        |false     | need cert-manager, will use https to serve url. not support Wildcard Domain 
PLUGIN_K8S_URL     |-     | k8s api server url  
PLUGIN_K8S_TOKEN   |-     | k8s service account 


### Drone CI Usage:

```yml
kind: pipeline
name: default
steps:
  - name: notify
    image: simcu/k8s-deploy
    settings:
      namespace: deploytest
      name: test
      environment: staging
      image: nginx:alpine
      cpu: 1000m
      mem: 1024Mi
      rsvp: false
      port: 80
      url: test.simcu.com
      acme: true
      k8s_url: https://xx.xx.xx.xx:8443/
      k8s_token: abcdefghijklmn12345678
```

If you want to use it as simple docker container, only set -e with the Enviroment. e.g.
> docker run -d --rm -e PLUGIN_NAMESPACE=test -e ..... simcu/k8s-deploy