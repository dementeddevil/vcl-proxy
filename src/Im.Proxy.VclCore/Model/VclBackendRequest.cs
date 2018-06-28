using System;
using System.Collections;
using System.Collections.Generic;

namespace Im.Proxy.VclCore.Model
{
    public class VclBackendRequest
    {
        public string Method { get; set; }

        public string Uri { get; set; }

        public IDictionary<string, string> Headers { get; } =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }
}