using System.Net;

namespace Im.Proxy.VclCore.Model
{
    public class VclClient
    {
        public IPAddress Ip { get; set; }

        public int Port { get; set; }

        public string Identity { get; set; }
    }
}