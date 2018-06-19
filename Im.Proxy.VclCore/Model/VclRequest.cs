using System.Collections.Generic;

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
        /// Gets or sets the HTTP protocol version.
        /// </summary>
        /// <value>
        /// The proto.
        /// </value>
        public string ProtocolVersion { get; set; }

        /// <summary>
        /// Gets or sets the backend.
        /// </summary>
        /// <value>
        /// The backend.
        /// </value>
        public VclBackend Backend { get; set; }

        /// <summary>
        /// Gets or sets the HTTP.
        /// </summary>
        /// <value>
        /// The HTTP.
        /// </value>
        public IDictionary<string, string> Headers { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether [hash always miss].
        /// </summary>
        /// <value>
        ///   <c>true</c> if [hash always miss]; otherwise, <c>false</c>.
        /// </value>
        public bool HashAlwaysMiss { get; set; }

        public bool HashIgnoreBusy { get; set; }
    }
}
