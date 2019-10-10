using System;
namespace Emilia
{
    public class Plugin
    {
        public string Namespace { get; set; }
        public string Name { get; set; }
        public string Environment { get; set; }
        public string Image { get; set; }
        public string Cpu { get; set; }
        public string Mem { get; set; }
        public bool Rsvp { get; set; }
        public int Port { get; set; }
        public string ServiceType { get; set; }
        public string Url { get; set; }
        public bool Acme { get; set; }
        public string K8SUrl { get; set; }
        public string K8SToken { get; set; }
        public string RegistrySecret { get; set; }
        public bool Debug { get; set; } = false;
    }
}
