using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Im.Proxy.VclCore.Model;

namespace Im.Proxy.VclCore
{
    public class VclProbe
    {
        private int? _initial;

        public VclProbe(string name = null)
        {
            Name = name ?? "inline";
        }

        public string Name { get; }

        public string Url { get; set; } = "/";

        public int ExpectedResponse { get; set; } = 200;

        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(2);

        public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(5);

        public int Initial { get => _initial ?? Threshold - 1; set => _initial = value; }

        public int Window { get; set; } = 8;

        public int Threshold { get; set; } = 3;

        public void Initialise(VclBackend backend)
        {
            // Setup the initial number of healthy responses
            backend.HealthCheckHistory.Clear();
            if (Initial > 0)
            {
                for (var loop = 0; loop < Initial; ++loop)
                {
                    backend.HealthCheckHistory.Enqueue(true);
                }
            }
        }

        public async Task Execute(VclBackend backend)
        {
            // Issue probe request
            var httpClient = backend.Client;
            httpClient.Timeout = Timeout;
            var responseMessage = await httpClient
                .GetAsync(Url)
                .ConfigureAwait(false);

            // Update backend with probe result
            AddProbeResult(backend, (int) responseMessage.StatusCode == ExpectedResponse);
        }

        private void AddProbeResult(VclBackend backend, bool healthy)
        {
            backend.HealthCheckHistory.Enqueue(healthy);

            while (backend.HealthCheckHistory.Count > Window)
            {
                backend.HealthCheckHistory.Dequeue();
            }

            backend.Healthy = backend.HealthCheckHistory.Count(v => v) >= Threshold;
        }
    }
}

