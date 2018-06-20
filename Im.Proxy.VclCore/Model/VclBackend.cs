using System;
using System.Collections.Generic;
using System.Net.Http;

namespace Im.Proxy.VclCore.Model
{
    /// <summary>
    /// 
    /// </summary>
    /// <remarks>
    /// Represents a backend server
    /// </remarks>
    public class VclBackend
    {
        public VclBackend(string host, int port)
        {
            Host = host;
            Port = port;

            Client.BaseAddress = 
                new UriBuilder(
                    port == 443 ? "https:" : "http:",
                    host,
                    port).Uri;
        }

        public string Host { get; }

        public int Port { get; }

        public bool Healthy { get; set; }

        public Queue<bool> HealthCheckHistory { get; } = new Queue<bool>();

        public HttpClient Client { get; } = new HttpClient();
    }
}