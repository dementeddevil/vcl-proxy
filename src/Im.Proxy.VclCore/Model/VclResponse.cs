using System;
using System.Collections.Generic;
using System.IO;

namespace Im.Proxy.VclCore.Model
{
    public class VclResponse
    {
        public int StatusCode { get; set; }

        public string StatusDescription { get; set; }

        public IDictionary<string, string> Headers { get; } =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public Stream Body { get; private set; }

        public void CopyBodyFrom(Stream body)
        {
            var stream = new MemoryStream();
            body.CopyTo(stream);
            stream.Position = 0;
            Body = stream;
        }
    }
}