using System;
using KubeClient;
using KubeClient.Models;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Emilia
{
    public class Kubernetes
    {
        private IKubeApiClient _kubeApiClient { get; }

        public Kubernetes(IKubeApiClient kubeApiClient)
        {
            _kubeApiClient = kubeApiClient;
        }

        public void CheckAndCreateNamespace(string name)
        {
            
            if (_kubeApiClient.NamespacesV1().Get(name).Result == null)
            {
                var res = _kubeApiClient.NamespacesV1().Create(new NamespaceV1
                {
                    Metadata = new ObjectMetaV1
                    {
                        Name = name,
                    }
                }).Result;
                Log($"Namespace: {name} not found, created");
            }
            else
            {
                Log($"Namespace: {name} already exists");
            }

        }

        public void UpdateDeployment(string ns, string name, string env, string image, string cpu, string mem, bool rsvp, int port, string registrySecret)
        {
            var deploy = _kubeApiClient.DeploymentsV1().Get($"{name}-{env}", ns).Result;
            if (deploy == null)
            {
                Log($"Deployment: {name}-{env} not found, created");


                var templateContainer = new ContainerV1
                {
                    Name = $"{name}-{env}",
                    Image = image,
                    Resources = new ResourceRequirementsV1
                    {
                        Limits =
                        {
                            ["cpu"] = cpu,
                            ["memory"] = mem
                        }
                    }
                };

                if (rsvp)
                {
                    templateContainer.Resources.Requests.Add("cpu", cpu);
                    templateContainer.Resources.Requests.Add("memory", mem);
                }

                if (port != 0)
                {
                    templateContainer.Ports.Add(new ContainerPortV1
                    {
                        Name = "tcp",
                        ContainerPort = port
                    });
                }

                var res = _kubeApiClient.DeploymentsV1Beta1().Create(new DeploymentV1Beta1
                {
                    ApiVersion= "apps/v1beta1",
                    Metadata = new ObjectMetaV1
                    {
                        Name = $"{name}-{env}",
                        Namespace = ns,
                        Labels =
                        {
                            ["simcu-deploy-app"]=$"{ns}-{name}-{env}"
                        }
                    },
                    Spec = new DeploymentSpecV1Beta1
                    {
                        Replicas = 1,
                        Selector = new LabelSelectorV1
                        {
                            MatchLabels =
                            {
                                ["simcu-deploy-app"]= $"{ns}-{name}-{env}"
                            }
                        },

                        Template = new PodTemplateSpecV1
                        {
                            Metadata = new ObjectMetaV1
                            {
                                Labels =
                                {
                                    ["simcu-deploy-app"] = $"{ns}-{name}-{env}"
                                }
                            },
                            Spec = new PodSpecV1
                            {
                                Containers = { templateContainer },
                                ImagePullSecrets = { new LocalObjectReferenceV1 { Name = registrySecret } }
                            }
                        }
                    }
                }).Result;
            }
            else
            {
                Log($"Deployment: {name}-{env} already exists, updated");
                var res = _kubeApiClient.DeploymentsV1Beta1().Update($"{name}-{env}", kubeNamespace: ns, patchAction: patch =>
                  {
                      patch.Replace(x => x.Spec.Template.Spec.Containers[0].Image, image);
                      patch.Replace(x => x.Spec.Template.Spec.ImagePullSecrets, new List<LocalObjectReferenceV1> { new LocalObjectReferenceV1 { Name = registrySecret } });
                      patch.Replace(x => x.Spec.Template.Spec.Containers[0].Resources.Limits, new Dictionary<string, string> { { "cpu", cpu }, { "memory", mem } });
                      if (rsvp)
                      {
                          patch.Replace(x => x.Spec.Template.Spec.Containers[0].Resources.Requests, new Dictionary<string, string> { { "cpu", cpu }, { "memory", mem } });
                      }
                      if (port != 0)
                      {
                          patch.Replace(x => x.Spec.Template.Spec.Containers[0].Ports, new ContainerPortV1
                          {
                              Name = "tcp",
                              ContainerPort = port
                          });
                      }
                  }).Result;
            }
        }

        public void UpdateService(string ns, string name, string env, string type, int port)
        {
            if (_kubeApiClient.ServicesV1().Get($"{name}-{env}", ns).Result == null)
            {
                Log($"Service: {name}-{env} not found, created");
                var res = _kubeApiClient.ServicesV1().Create(new ServiceV1
                {
                    Metadata = new ObjectMetaV1
                    {
                        Name = $"{name}-{env}",
                        Namespace = ns
                    },
                    Spec = new ServiceSpecV1
                    {
                        Type = type,
                        Selector =
                        {
                            ["simcu-deploy-app"] = $"{ns}-{name}-{env}"
                        },
                        Ports =
                        {
                            new ServicePortV1
                            {
                                Port = port,
                                TargetPort = port,
                                Name = $"tcp-{port}"
                            }
                        }
                    }
                }).Result;
            }
            else
            {
                Log($"Service: {name}-{env} already exists, updated");
                var res = _kubeApiClient.ServicesV1().Update($"{name}-{env}", patch =>
                {
                    patch.Replace(x => x.Spec.Type, type);
                    patch.Replace(x => x.Spec.Ports, new List<ServicePortV1> { new ServicePortV1 { Name = $"tcp-{port}", Port = port, TargetPort = port } });
                }, ns).Result;
            }
        }

        public void UpdateIngress(string ns, string name, string env, string url, int port, bool acme)
        {
            var spec = new IngressSpecV1Beta1
            {
                Rules =
                    {
                        new IngressRuleV1Beta1
                        {
                            Host = url,
                            Http = new HTTPIngressRuleValueV1Beta1
                            {
                                Paths =
                                {
                                    new HTTPIngressPathV1Beta1
                                    {
                                        Backend = new IngressBackendV1Beta1{
                                            ServiceName = $"{name}-{env}",
                                            ServicePort = port
                                        },
                                    }
                                }
                            }
                        }
                    }
            };
            if (acme)
            {
                spec.Tls.Add(
                    new IngressTLSV1Beta1
                    {
                        Hosts = { url },
                        SecretName = $"acme-{name}-{env}"
                    }
                );
            }
            var meta = new ObjectMetaV1
            {
                Name = $"{name}-{env}",
                Namespace = ns,
                Annotations =
                        {
                            ["kubernetes.io/tls-acme"] = acme.ToString()
                        }
            };
            var ingress = _kubeApiClient.IngressesV1Beta1().Get($"{name}-{env}", ns).Result;
            if (ingress == null)
            {
                Log($"Ingress: {name}-{env} -> {url} not found, created");

                var res = _kubeApiClient.IngressesV1Beta1().Create(new IngressV1Beta1
                {
                    Metadata = meta,
                    Spec = spec
                }).Result;
            }
            else
            {
                Log($"Ingress: {name}-{env} -> {url} already exists, updated");
                var res = _kubeApiClient.IngressesV1Beta1().Update($"{name}-{env}", patch =>
                {
                    patch.Replace(x => x.Spec, spec);
                    patch.Replace(x => x.Metadata, meta);
                }, ns).Result;
            }
        }

        static void Log(string log, string type = "Info")
        {
            Console.WriteLine($"[{DateTime.Now.ToString()}]{type}: {log}");
        }
    }
}
