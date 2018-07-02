using System.Net;

namespace Im.Proxy.VclCore.Model
{
    public class VclServer
    {
        public string HostName { get; set; }

        public string Identity { get; set; }

        public IPAddress Ip { get; set; }

        public int Port { get; set; }
    }
}