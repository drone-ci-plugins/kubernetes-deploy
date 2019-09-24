#!/bin/bash

#测试用的变量
# PLUGIN_NAMESPACE=xraintest
# PLUGIN_NAME=nginxtest

# PLUGIN_ENVIROMENT=production
# PLUGIN_IMAGE=nginx
# PLUGIN_CPU=500m
# PLUGIN_MEM=1024Mi
# PLUGIN_RSVP=true
# PLUGIN_PORT=80
# PLUGIN_SERVICE_TYPE=ClusterIP
# PLUGIN_URL=test.xrain.zhoushijt.com
# PLUGIN_ACME=false
# PLUGIN_K8S_URL=https://k8s.zhoushijt.com/k8s/clusters/c-5lgdn
# PLUGIN_K8S_TOKEN=kubeconfig-u-7nq75.c-5lgdn:qzgg79d2f9pcm466fdchkq4l4d6rbncfvkbznxcpnzsr5xg459vp7g
# PLUGIN_K8S_CA=NOTAFILEPATHISFILECONTENT
# REGISTRY_SECRET_NAME=none

function makeNameSpaceTmpl(){
	eval "cat <<EOF
`cat templates/namespace.yml`
EOF">auto/namespace.yml
}

function makeDeploymentTmpl(){
if [ "$PLUGIN_RSVP" == "true" ];then
	eval "cat <<EOF
`cat templates/deployment-rsvp.yml`
EOF">auto/deployment.yml
else
	eval "cat <<EOF
`cat templates/deployment.yml`
EOF">auto/deployment.yml
fi
}

function makeServiceTmpl(){
	eval "cat <<EOF
`cat templates/service.yml`
EOF">auto/service.yml
}

function makeIngressTmpl(){
if [ "$PLUGIN_ACME" == "true" ];then
URL_SECRET_NAME=${PLUGIN_URL//./-}
URL_SECRET_NAME=${URL_SECRET_NAME//\*/xx}
	eval "cat <<EOF
`cat templates/ingress-tls.yml`
EOF">auto/ingress.yml
else
	eval "cat <<EOF
`cat templates/ingress.yml`
EOF">auto/ingress.yml
fi
}


if [ ! -d "auto" ]; then
  mkdir auto
fi

if [ -z "$PLUGIN_NAMESPACE" ];then
	PLUGIN_NAMESPACE=$DRONE_REPO_NAMESPACE
fi
if [ -z "$PLUGIN_NAME" ];then
	PLUGIN_NAME=${DRONE_REPO/$DRONE_REPO_NAMESPACE\//}
fi
if [ -z "$PLUGIN_NAMESPACE" ];then
	echo "[ERROR] namespace isn't defined!"
	exit;
fi
if [ -z "$PLUGIN_NAME" ];then
	echo "[ERROR] name isn't defined!"
	exit;
fi
if [ -z "$PLUGIN_IMAGE" ];then
	echo "[ERROR] image isn't defined!"
	exit;
fi

makeNameSpaceTmpl
makeDeploymentTmpl

if [ ! "$PLUGIN_PORT" = "1" ];then
	makeServiceTmpl
fi

if [ ! -z "$PLUGIN_URL" ];then
	if [ "$PLUGIN_PORT" = "1" ];then
		echo "[ERROR] url must define with port defined!"
		exit;
	fi
	makeIngressTmpl
fi

if [ "$DEBUG" == "true" ];then
echo '<============== namespace.yml ==============>'
cat auto/namespace.yml
echo '<============== deployment.yml ==============>'
cat auto/deployment.yml
echo '<============== service.yml ==============>'
if [ -f "auto/service.yml" ];then
	cat auto/service.yml
fi
echo '<============== ingress.yml ==============>'
if [ -f "auto/ingress.yml" ];then
	cat auto/ingress.yml
fi

echo '*******************Start Deploy*******************'
fi
if [ -z "$PLUGIN_K8S_CA" ];then
	kubectl --server=$PLUGIN_K8S_URL --token=$PLUGIN_K8S_TOKEN apply -f auto
else
	echo $PLUGIN_K8S_CA | base64 -d > ca.crt
	kubectl --server=$PLUGIN_K8S_URL --token=$PLUGIN_K8S_TOKEN --certificate-authority=ca.crt apply -f auto
fi





