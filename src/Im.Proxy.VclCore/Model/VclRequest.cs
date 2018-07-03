using System;
using System.Collections.Generic;
using System.IO;

namespace Im.Proxy.VclCore.Model
{
    /// <summary>
    /// 
    /// </summary>
    /// <remarks>
    /// This class is surfaced in VCL evaluation as an object called "req"
    /// </remarks>
    public class VclRequest
    {
        private int _requestHash = 185;

        public string RequestId { get; } = Guid.NewGuid().ToString("N");

        /// <summary>
        /// Gets or sets the HTTP method of the request.
        /// </summary>
        /// <value>
        /// The request.
        /// </value>
        public string Method { get; set; }

        /// <summary>
        /// Gets or sets the URL of the request.
        /// </summary>
        /// <value>
        /// The URL.
        /// </value>
        public string Url { get; set; }

        /// <summary>
        /// Gets or sets the HTTP.
        /// </summary>
        /// <value>
        /// The HTTP.
        /// </value>
        public IDictionary<string, string> Headers { get; set; }

        /// <summary>
        /// Gets or sets the HTTP protocol version.
        /// </summary>
        /// <value>
        /// The proto.
        /// </value>
        public string ProtocolVersion { get; set; }

        /// <summary>
        /// Gets or sets the hash.
        /// </summary>
        /// <value>
        /// The hash.
        /// </value>
        public string Hash { get; set; }

        /// <summary>
        /// Gets or sets the backend.
        /// </summary>
        /// <value>
        /// The backend.
        /// </value>
        public VclBackend Backend { get; set; }

        /// <summary>
        /// Gets or sets the restarts.
        /// </summary>
        /// <value>
        /// The restarts.
        /// </value>
        public int Restarts { get; set; }

        /// <summary>
        /// Gets or sets the esi level.
        /// </summary>
        /// <value>
        /// The esi level.
        /// </value>
        public int EsiLevel { get; set; }

        /// <summary>
        /// Gets or sets the TTL.
        /// </summary>
        /// <value>
        /// The TTL.
        /// </value>
        public TimeSpan Ttl { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this instance can gzip.
        /// </summary>
        /// <value>
        ///   <c>true</c> if this instance can gzip; otherwise, <c>false</c>.
        /// </value>
        /// <remarks>
        /// true if the client provided the gzip or x-gzip in the accept-encoding header
        /// </remarks>
        public bool CanGzip { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether [hash always miss].
        /// </summary>
        /// <value>
        ///   <c>true</c> if [hash always miss]; otherwise, <c>false</c>.
        /// </value>
        public bool HashAlwaysMiss { get; set; }

        public bool HashIgnoreBusy { get; set; }

        public Stream Body { get; private set; }

        public void CopyBodyFrom(Stream body)
        {
            var stream = new MemoryStream();
            body.CopyTo(stream);
            Body = stream;
        }

        public void AddToHash(object value)
        {
            // Combine the hash code and update the request hash value
            var nullValue = "DummyNullValue".GetHashCode();
            _requestHash = (_requestHash << 5) | (value?.GetHashCode() ?? nullValue);
            Hash = $"RequestHash:{_requestHash}";
        }
    }
}
