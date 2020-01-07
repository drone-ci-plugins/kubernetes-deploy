using System;
using KubeClient;
using KubeClient.Models;
using System.Collections.Generic;

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

        public void UpdateDeployment(string ns, string name, string env, string image, string cpu, string mem,
            bool rsvp, int port, string registrySecret, Plugin config)
        {
            var deploy = _kubeApiClient.DeploymentsV1().Get($"{name}-{env}", ns).Result;
            if (deploy == null)
            {
                Log($"Deployment: {name}-{env} not found, created");


                var templateContainer = new ContainerV1
                {
                    Name = $"{name}-{env}",
                    Image = image,
                };

                if (!string.IsNullOrEmpty(cpu))
                {
                    templateContainer.Resources = new ResourceRequirementsV1
                    {
                        Limits =
                        {
                            ["cpu"] = cpu
                        }
                    };
                }

                if (!string.IsNullOrEmpty(mem))
                {
                    templateContainer.Resources = new ResourceRequirementsV1
                    {
                        Limits =
                        {
                            ["memory"] = mem
                        }
                    };
                }

                if (!string.IsNullOrEmpty(config.EntryPoint))
                {
                    templateContainer.Command.Add(config.EntryPoint);
                }

                if (!string.IsNullOrEmpty(config.Command))
                {
                    templateContainer.Args.Add(config.Command);
                }

                if (rsvp)
                {
                    if (!string.IsNullOrEmpty(cpu))
                    {
                        templateContainer.Resources.Requests.Add("cpu", cpu);
                    }

                    if (!string.IsNullOrEmpty(mem))
                    {
                        templateContainer.Resources.Requests.Add("memory", mem);
                    }
                }

                if (port > 0)
                {
                    templateContainer.Ports.Add(new ContainerPortV1
                    {
                        Name = "tcp",
                        ContainerPort = port
                    });
                }

                var res = _kubeApiClient.DeploymentsV1().Create(new DeploymentV1()
                {
                    ApiVersion = "apps/v1beta1",
                    Metadata = new ObjectMetaV1
                    {
                        Name = $"{name}-{env}",
                        Namespace = ns,
                        Labels =
                        {
                            ["simcu-deploy-app"] = $"{ns}-{name}-{env}"
                        }
                    },
                    Spec = new DeploymentSpecV1()
                    {
                        Replicas = 1,
                        Selector = new LabelSelectorV1
                        {
                            MatchLabels =
                            {
                                ["simcu-deploy-app"] = $"{ns}-{name}-{env}"
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
                                Containers = {templateContainer},
                                ImagePullSecrets = {new LocalObjectReferenceV1 {Name = registrySecret}}
                            }
                        }
                    }
                }).Result;
            }
            else
            {
                Log($"Deployment: {name}-{env} already exists, updated");
                var res = _kubeApiClient.DeploymentsV1().Update($"{name}-{env}", kubeNamespace: ns,
                    patchAction: patch =>
                    {
                        patch.Replace(x => x.Spec.Template.Spec.Containers[0].Image, image);
                        patch.Replace(x => x.Spec.Template.Spec.ImagePullSecrets,
                            new List<LocalObjectReferenceV1> {new LocalObjectReferenceV1 {Name = registrySecret}});
                        patch.Replace(x => x.Spec.Template.Spec.Containers[0].Resources.Limits,
                            new Dictionary<string, string> {{"cpu", cpu}, {"memory", mem}});

                        patch.Replace(x => x.Metadata.Annotations, config.Annotations);
                        config.Labels.Add("simcu-deploy-app", $"{ns}-{name}-{env}");
                        patch.Replace(x => x.Metadata.Labels, config.Labels);

                        if (!string.IsNullOrEmpty(config.EntryPoint))
                        {
                            if (deploy.Spec.Template.Spec.Containers[0].Command.Count > 0)
                            {
                                patch.Replace(x => x.Spec.Template.Spec.Containers[0].Command,
                                    new List<string> {config.EntryPoint});
                            }
                            else
                            {
                                patch.Add(x => x.Spec.Template.Spec.Containers[0].Command,
                                    new List<string> {config.EntryPoint});
                            }
                        }
                        else
                        {
                            if (deploy.Spec.Template.Spec.Containers[0].Command.Count > 0)
                            {
                                patch.Remove(x => x.Spec.Template.Spec.Containers[0].Command);
                            }
                        }

                        if (!string.IsNullOrEmpty(config.Command))
                        {
                            if (deploy.Spec.Template.Spec.Containers[0].Args.Count > 0)
                            {
                                patch.Replace(x => x.Spec.Template.Spec.Containers[0].Args,
                                    new List<string> {config.Command});
                            }
                            else
                            {
                                patch.Add(x => x.Spec.Template.Spec.Containers[0].Args,
                                    new List<string> {config.Command});
                            }
                        }
                        else
                        {
                            if (deploy.Spec.Template.Spec.Containers[0].Args.Count > 0)
                            {
                                patch.Remove(x => x.Spec.Template.Spec.Containers[0].Args);
                            }
                        }

                        if (rsvp)
                        {
                            patch.Replace(x => x.Spec.Template.Spec.Containers[0].Resources.Requests,
                                new Dictionary<string, string> {{"cpu", cpu}, {"memory", mem}});
                        }
                        else
                        {
                            if (deploy.Spec.Template.Spec.Containers[0].Resources.Requests.Count > 0)
                            {
                                patch.Remove(x => x.Spec.Template.Spec.Containers[0].Resources.Requests);
                            }
                        }

                        if (port != 0)
                        {
                            patch.Add(x => x.Spec.Template.Spec.Containers[0].Ports, new List<ContainerPortV1>
                            {
                                new ContainerPortV1
                                {
                                    Name = $"tcp-{port}",
                                    ContainerPort = port
                                }
                            });
                        }
                        else
                        {
                            if (deploy.Spec.Template.Spec.Containers[0].Ports.Count > 0)
                            {
                                patch.Remove(x => x.Spec.Template.Spec.Containers[0].Ports, 0);
                            }
                        }
                    }).Result;
            }
        }

        public void UpdateService(string ns, string name, string env, string type, int port)
        {
            var svc = _kubeApiClient.ServicesV1().Get($"{name}-{env}", ns).Result;
            if (port > 0)
            {
                if (svc == null)
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
                                    Name = $"tcp-{port}"
                                }
                            }
                        }
                    }).Result;
                }
                else
                {
                    Log($"Service: {name}-{env} already exists, Updated");
                    var res = _kubeApiClient.ServicesV1().Update($"{name}-{env}", patch =>
                    {
                        patch.Replace(x => x.Spec.Type, type);
                        patch.Replace(x => x.Spec.Ports,
                            new ServicePortV1 {Name = $"tcp-{port}", Port = port, TargetPort = port}, 0);
                    }, ns).Result;
                }
            }
            else
            {
                var res = _kubeApiClient.ServicesV1().Delete($"{name}-{env}", ns).Result;
                Log($"Service: PLUGIN_PORT not set,Clean Service");
            }
        }

        public void UpdateIngress(string ns, string name, string env, string url, int port, bool acme)
        {
            if (port > 0 && !string.IsNullOrEmpty(url))
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
                                        Backend = new IngressBackendV1Beta1
                                        {
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
                            Hosts = {url},
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
                    Log($"Ingress: {name}-{env} -> {url} already exists, Updated");
                    var res = _kubeApiClient.IngressesV1Beta1().Update($"{name}-{env}", patch =>
                    {
                        patch.Replace(x => x.Spec, spec);
                        patch.Replace(x => x.Metadata, meta);
                    }, ns).Result;
                }
            }
            else
            {
                var res = _kubeApiClient.IngressesV1Beta1().Delete($"{name}-{env}", ns).Result;
                Log($"Ingress: PLUGIN_PORT or PLUGIN_URL not set,Clean Ingress");
            }
        }

        static void Log(string log, string type = "Info")
        {
            Console.WriteLine($"[{DateTime.Now.ToString()}]{type}: {log}");
        }
    }
}