using System;
using System.Collections.Generic;
using System.Net.Http;

namespace Im.Proxy.VclCore.Model
{
    /// <summary>
    /// <c>VclBackend</c> represents a backend server resource
    /// </summary>
    public class VclBackend
    {
        private HttpClient _httpClient;

        public string Host { get; set; }

        public string Port { get; set; }

        public string HostHeader { get; set; }

        public TimeSpan ConnectTimeout { get; set; }

        public TimeSpan FirstByteTimeout { get; set; }

        public TimeSpan BetweenBytesTimeout { get; set; }

        public string ProxyHeader { get; set; }

        public VclProbe Probe { get; set; }

        public int MaxConnections { get; set; }

        public bool Healthy { get; set; }

        public HttpClient Client
        {
            get
            {
                if (_httpClient == null)
                {
                    _httpClient =
                        new HttpClient
                        {
                            BaseAddress = new UriBuilder(Port + ":", Host).Uri
                        };
                }

                return _httpClient;
            }
        }

        public void Initialise()
        {
            Probe.Initialise(this);
        }
    }
}