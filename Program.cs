using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography.X509Certificates;
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
                Environment = Environment.GetEnvironmentVariable("PLUGIN_ENVIRONMENT") ?? "production",
                Image = Environment.GetEnvironmentVariable("PLUGIN_IMAGE"),
                Cpu = Environment.GetEnvironmentVariable("PLUGIN_CPU"),
                Mem = Environment.GetEnvironmentVariable("PLUGIN_MEM"),
                Rsvp = Environment.GetEnvironmentVariable("PLUGIN_RSVP") == "false",
                Port = Environment.GetEnvironmentVariable("PLUGIN_PORT") == null
                    ? 0
                    : int.Parse(Environment.GetEnvironmentVariable("PLUGIN_PORT")),
                ServiceType = Environment.GetEnvironmentVariable("PLUGIN_SERVICE_TYPE") ?? "ClusterIP",
                Url = Environment.GetEnvironmentVariable("PLUGIN_URL"),
                Acme = Environment.GetEnvironmentVariable("PLUGIN_ACME") == "false",
                K8SUrl = Environment.GetEnvironmentVariable("PLUGIN_K8S_URL"),
                K8SToken = Environment.GetEnvironmentVariable("PLUGIN_K8S_TOKEN"),
                RegistrySecret = Environment.GetEnvironmentVariable("PLUGIN_REGISTRY_SECRET") ?? "simcu",
                Debug = Environment.GetEnvironmentVariable("PLUGIN_DEBUG") == "true",
                //New:
                EntryPoint = Environment.GetEnvironmentVariable("PLUGIN_ENTRY_POINT"),
                Command = Environment.GetEnvironmentVariable("PLUGIN_COMMAND"),
                Labels = new Dictionary<string, string>(),
                Annotations = new Dictionary<string, string>()
            };

            if (config.Namespace == null)
            {
                config.Namespace = Environment.GetEnvironmentVariable("DRONE_REPO_NAMESPACE");
            }

            if (config.Name == null)
            {
                config.Name = Environment.GetEnvironmentVariable("DRONE_REPO_NAME");
            }

            var kubeOptions = new KubeClientOptions();
            if (config.K8SUrl == null)
            {
                if (Environment.GetEnvironmentVariable("KUBERNETES_SERVICE_HOST") != null &&
                    Environment.GetEnvironmentVariable("KUBERNETES_SERVICE_PORT") != null)
                {
                    var k8s =
                        $"https://{Environment.GetEnvironmentVariable("KUBERNETES_SERVICE_HOST")}:{Environment.GetEnvironmentVariable("KUBERNETES_SERVICE_PORT")}/";
                    kubeOptions = new KubeClientOptions
                    {
                        ApiEndPoint = new Uri(k8s),
                        AuthStrategy = KubeAuthStrategy.BearerToken,
                        AccessToken = File.ReadAllText("/var/run/secrets/kubernetes.io/serviceaccount/token"),
                        CertificationAuthorityCertificate =
                            new X509Certificate2(
                                File.ReadAllText("/var/run/secrets/kubernetes.io/serviceaccount/ca.crt")
                            )
                    };
                }
                else
                {
                    Log("K8S Cluster Url isn't defined");
                }
            }
            else if (config.K8SToken == null)
            {
                Log("K8S Cluster Token isn't defined");
            }
            else
            {
                kubeOptions = new KubeClientOptions
                {
                    ApiEndPoint = new Uri(config.K8SUrl),
                    AccessToken = config.K8SToken,
                    AuthStrategy = KubeAuthStrategy.BearerToken,
                    AllowInsecure = true
                };
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
                Log("Image isn't defined");
            }

            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PLUGIN_LABLES")))
            {
                foreach (var item in Environment.GetEnvironmentVariable("PLUGIN_LABLES").Split('|'))
                {
                    var itemArr = item.Split('=', 2);
                    if (itemArr.Length == 2)
                    {
                        config.Labels.Add(itemArr[0], itemArr[1]);
                    }
                }
            }

            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PLUGIN_ANNOTATIONS")))
            {
                foreach (var item in Environment.GetEnvironmentVariable("PLUGIN_ANNOTATIONS").Split('|'))
                {
                    var itemArr = item.Split('=', 2);
                    if (itemArr.Length == 2)
                    {
                        config.Annotations.Add(itemArr[0], itemArr[1]);
                    }
                }
            }


            var kube = new Kubernetes(KubeApiClient.Create(kubeOptions));

            kube.CheckAndCreateNamespace(config.Namespace);
            kube.UpdateDeployment(config.Namespace, config.Name, config.Environment, config.Image, config.Cpu,
                config.Mem, config.Rsvp, config.Port, config.RegistrySecret, config);
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