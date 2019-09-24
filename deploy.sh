#!/bin/bash

function makeNameSpaceTmpl(){
	eval "cat <<EOF
`cat /home/templates/namespace.yml`
EOF">/home/auto/namespace.yml
}

function makeDeploymentTmpl(){
if [ "$PLUGIN_RSVP" == "true" ];then
	eval "cat <<EOF
`cat /home/templates/deployment-rsvp.yml`
EOF">/home/auto/deployment.yml
else
	eval "cat <<EOF
`cat /home/templates/deployment.yml`
EOF">/home/auto/deployment.yml
fi
}

function makeServiceTmpl(){
	eval "cat <<EOF
`cat /home/templates/service.yml`
EOF">/home/auto/service.yml
}

function makeIngressTmpl(){
if [ "$PLUGIN_ACME" == "true" ];then
URL_SECRET_NAME=${PLUGIN_URL//./-}
URL_SECRET_NAME=${URL_SECRET_NAME//\*/xx}
	eval "cat <<EOF
`cat /home/templates/ingress-tls.yml`
EOF">/home/auto/ingress.yml
else
	eval "cat <<EOF
`cat /home/templates/ingress.yml`
EOF">/home/auto/ingress.yml
fi
}


if [ ! -d "auto" ]; then
  mkdir /home/auto
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
cat /home/auto/namespace.yml
echo '<============== deployment.yml ==============>'
cat /home/auto/deployment.yml
echo '<============== service.yml ==============>'
if [ -f "auto/service.yml" ];then
	cat /home/auto/service.yml
fi
echo '<============== ingress.yml ==============>'
if [ -f "auto/ingress.yml" ];then
	cat /home/auto/ingress.yml
fi

echo '*******************Start Deploy*******************'
fi
if [ -z "$PLUGIN_K8S_CA" ];then
	kubectl --server=$PLUGIN_K8S_URL --token=$PLUGIN_K8S_TOKEN apply -f /home/auto
else
	echo $PLUGIN_K8S_CA | base64 -d > ca.crt
	kubectl --server=$PLUGIN_K8S_URL --token=$PLUGIN_K8S_TOKEN --certificate-authority=ca.crt apply -f /home/auto
fi





