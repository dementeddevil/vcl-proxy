using System;
using System.Collections.Generic;

namespace Im.Proxy.VclCore.Model
{
    public class VclResponse
    {
        public int StatusCode { get; set; }

        public string StatusDescription { get; set; }

        public IDictionary<string, string> Headers { get; } =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }
}