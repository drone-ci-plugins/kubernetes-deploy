﻿using System;
using KubeClient;

namespace Emilia
{
    class Program
    {
        static void Main(string[] args)
        {
            var config = new Plugin
            {
                Namespace = Environment.GetEnvironmentVariable("PLUGIN_NAMESPACE"),
                Name = Environment.GetEnvironmentVariable("PLUGIN_NAME"),
                Environment = Environment.GetEnvironmentVariable("PLUGIN_ENVIROMENT") ?? "production",
                Image = Environment.GetEnvironmentVariable("PLUGIN_IMAGE"),
                Cpu = Environment.GetEnvironmentVariable("PLUGIN_CPU") ?? "500m",
                Mem = Environment.GetEnvironmentVariable("PLUGIN_MEM") ?? "1024Mi",
                Rsvp = Environment.GetEnvironmentVariable("PLUGIN_RSVP") == "true",
                Port = Environment.GetEnvironmentVariable("PLUGIN_PORT") == null ? 0 : int.Parse(Environment.GetEnvironmentVariable("PLUGIN_PORT")),
                ServiceType = Environment.GetEnvironmentVariable("PLUGIN_SERVICE_TYPE") ?? "ClusterIP",
                Url = Environment.GetEnvironmentVariable("PLUGIN_URL"),
                Acme = Environment.GetEnvironmentVariable("PLUGIN_ACME") == "true",
                K8SUrl = Environment.GetEnvironmentVariable("PLUGIN_K8S_URL"),
                K8SToken = Environment.GetEnvironmentVariable("PLUGIN_K8S_TOKEN"),
                RegistrySecret = Environment.GetEnvironmentVariable("PLUGIN_REGISTRY_SECRET") ?? "simcu",
                Debug = Environment.GetEnvironmentVariable("PLUGIN_DEBUG") == "true"
            };

            if (config.Namespace == null)
            {
                config.Namespace = Environment.GetEnvironmentVariable("DRONE_REPO_NAMESPACE");
            }

            if (config.Name == null)
            {
                config.Name = Environment.GetEnvironmentVariable("DRONE_REPO_NAME");
            }

            if (config.K8SToken == null || config.K8SUrl == null)
            {
                Log("K8S Cluster isn't defined");
            }

            if (config.Namespace == null)
            {
                Log("Namespace isn't defined");
            }

            if (config.Name == null)
            {
                Log("Name isn't defined");
            }

            if (config.Image == null)
            {
                Log("Image isn't defind");
            }

            var kubeOptions = new KubeClientOptions
            {
                ApiEndPoint = new Uri(config.K8SUrl),
                AccessToken = config.K8SToken,
                AuthStrategy = KubeAuthStrategy.BearerToken,
                AllowInsecure = true
            };

            var kube = new Kubernetes(KubeApiClient.Create(kubeOptions));

            kube.CheckAndCreateNamespace(config.Namespace);
            kube.UpdateDeployment(config.Namespace, config.Name, config.Environment, config.Image, config.Cpu, config.Mem, config.Rsvp, config.Port, config.RegistrySecret);
            kube.UpdateService(config.Namespace, config.Name, config.Environment, config.ServiceType, config.Port);
            kube.UpdateIngress(config.Namespace, config.Name, config.Environment, config.Url, config.Port, config.Acme);
        }

        static void Log(string log, string type = "Error")
        {
            Console.WriteLine($"[{DateTime.Now.ToString()}]{type}: {log}");
            if (type == "Error")
            {
                Environment.Exit(128);
            }
        }
    }
}
