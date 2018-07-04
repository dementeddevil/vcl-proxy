using System;
using System.Collections.Generic;
using System.IO;

namespace Im.Proxy.VclCore.Model
{
    public class VclObject
    {
        public int StatusCode { get; set; }

        public string StatusDescription { get; set; }

        /// <summary>
        /// Gets or sets the HTTP header collection.
        /// </summary>
        /// <value>
        /// The HTTP.
        /// </value>
        public IDictionary<string, string> Headers { get; } =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Gets or sets the Time To Live for the response in seconds.
        /// </summary>
        /// <value>
        /// The TTL.
        /// </value>
        public int Ttl { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this <see cref="VclBackendResponse"/> is uncacheable.
        /// </summary>
        /// <value>
        /// <c>true</c> if uncacheable; otherwise, <c>false</c>.
        /// </value>
        public bool Uncacheable { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to do esi processing.
        /// </summary>
        /// <value>
        /// <c>true</c> if [do esi processing]; otherwise, <c>false</c>.
        /// </value>
        /// <remarks>
        /// Edge Side Includes allow an HTML page to be composed from
        /// multiple requests, each with a differing TTL.
        /// It is recommended that this only be switched on for text/html
        /// content.
        /// </remarks>
        public bool DoEsiProcessing { get; set; }

        public Stream Body { get; private set; }

        public DateTime LastUsedUtc { get; set; } = DateTime.UtcNow;

        public TimeSpan LastUsed => DateTime.UtcNow - LastUsedUtc;

        public void CopyBodyFrom(Stream body)
        {
            var stream = new MemoryStream();
            body.CopyTo(stream);
            stream.Position = 0;
            Body = stream;
        }
    }
}