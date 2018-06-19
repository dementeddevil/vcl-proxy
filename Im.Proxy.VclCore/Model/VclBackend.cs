namespace Im.Proxy.VclCore.Model
{
    /// <summary>
    /// 
    /// </summary>
    /// <remarks>
    /// Represents a backend server
    /// </remarks>
    public class VclBackend
    {
        public string Host { get; set; }

        public int Port { get; set; }

        public bool Healthy { get; set; }
    }
}