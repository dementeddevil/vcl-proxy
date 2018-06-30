namespace Im.Proxy.VclCore.Model
{
    public class VclContext
    {
        /// <summary>
        /// Gets an object that represents the local side of the connection.
        /// </summary>
        /// <value>
        /// The local.
        /// </value>
        public VclLocal Local { get; } = new VclLocal();

        /// <summary>
        /// Gets an object that represents the remote side of the connection.
        /// </summary>
        /// <value>
        /// The remote.
        /// </value>
        public VclRemote Remote { get; } = new VclRemote();

        /// <summary>
        /// Gets an object that represents the client.
        /// </summary>
        /// <value>
        /// The client.
        /// </value>
        public VclClient Client { get; } = new VclClient();

        /// <summary>
        /// Gets an object exposing information about the server.
        /// </summary>
        /// <value>
        /// The server.
        /// </value>
        public VclServer Server { get; } = new VclServer();

        /// <summary>
        /// Gets or sets the top request.
        /// </summary>
        /// <value>
        /// The top request is only valid during ESI processing.
        /// </value>
        public VclRequest TopRequest { get; set; } = null;

        /// <summary>
        /// Gets or sets the request.
        /// </summary>
        /// <value>
        /// The request.
        /// </value>
        public VclRequest Request { get; } = new VclRequest();

        /// <summary>
        /// Gets or sets the object returned from the caching layer.
        /// </summary>
        /// <value>
        /// The object.
        /// </value>
        public VclObject Object { get; set; } = new VclObject();

        public VclBackendRequest BackendRequest { get; set; }

        public VclBackendResponse BackendResponse { get; set; }

        public VclResponse Response { get; set; }
    }
}