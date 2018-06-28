using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Im.Proxy.VclCore.Model
{
    /// <summary>
    /// Helper class used to conduct health-check probe against a backend.
    /// </summary>
    /// <remarks>
    /// Probe instances are not shared between backend objects.
    /// Each backend receives it's own copy of a probe - even when a named
    /// probe has multiple references.
    /// </remarks>
    public class VclProbe
    {
        private VclBackend _backend;
        private readonly Queue<bool> _healthHistory = new Queue<bool>();
        private int? _initial;

        public VclProbe(string name)
        {
            Name = name;
        }

        public string Name { get; }

        public string Url { get; set; } = "/";

        public int ExpectedResponse { get; set; } = 200;

        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(2);

        public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(5);

        public int Initial { get => _initial ?? Threshold - 1; set => _initial = value; }

        public int Window { get; set; } = 8;

        public int Threshold { get; set; } = 3;

        public DateTime LastProbedWhenUtc { get; private set; } = DateTime.UtcNow;

        public DateTime NextProbeDueWhenUtc => LastProbedWhenUtc + Interval;

        public void Initialise(VclBackend backend)
        {
            // Cache the backend
            _backend = backend;

            // Setup the initial number of healthy responses
            _healthHistory.Clear();
            if (Initial > 0)
            {
                for (var loop = 0; loop < Initial; ++loop)
                {
                    _healthHistory.Enqueue(true);
                }
            }
        }

        public async Task Execute()
        {
            // Issue probe request
            var httpClient = _backend.Client;
            httpClient.Timeout = Timeout;
            var responseMessage = await httpClient
                .GetAsync(Url)
                .ConfigureAwait(false);

            // Update backend with probe result
            AddProbeResult((int) responseMessage.StatusCode == ExpectedResponse);

            // Update last probe timestamp
            LastProbedWhenUtc = DateTime.UtcNow;
        }

        private void AddProbeResult(bool healthy)
        {
            _healthHistory.Enqueue(healthy);

            while (_healthHistory.Count > Window)
            {
                _healthHistory.Dequeue();
            }

            _backend.Healthy = _healthHistory.Count(v => v) >= Threshold;
        }
    }
}

