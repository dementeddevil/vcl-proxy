namespace Im.Proxy.VclCore.Model
{
    public class VclContext
    {
        public VclLocal Local { get; set; } = new VclLocal();

        public VclRemote Remote { get; set; } = new VclRemote();

        public VclClient Client { get; set; } = new VclClient();

        public VclServer Server { get; set; } = new VclServer();

        public VclRequest Request { get; set; } = new VclRequest();

        public VclObject Object { get; set; } = new VclObject();

        public VclBackendRequest BackendRequest { get; set; }

        public VclBackendResponse BackendResponse { get; set; }

        public VclResponse Response { get; set; }
    }
}