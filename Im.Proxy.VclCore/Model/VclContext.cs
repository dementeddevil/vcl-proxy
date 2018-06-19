namespace Im.Proxy.VclCore.Model
{
    public class VclContext
    {
        public VclClient Client { get; set; } = new VclClient();

        public VclServer Server { get; set; } = new VclServer();

        public VclRequest Request { get; set; } = new VclRequest();

        public VclBackendRequest BackendRequest { get; set; }

        public VclBackendResponse BackendResponse { get; set; }

        public VclResponse Response { get; set; }
    }
}