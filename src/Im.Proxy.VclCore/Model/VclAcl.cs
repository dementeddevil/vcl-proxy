using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace Im.Proxy.VclCore.Model
{
    public class VclAcl
    {
        public VclAcl(string name)
        {
            Name = name;
        }

        public string Name { get; }

        public IList<VclAclEntry> Include { get; } = new List<VclAclEntry>();

        public IList<VclAclEntry> Exclude { get; } = new List<VclAclEntry>();

        public bool IsMatch(IPAddress client)
        {
            if (!Include.Any(e => e.IsMatch(client)))
            {
                return false;
            }

            if (Exclude.Any(e => e.IsMatch(client)))
            {
                return false;
            }

            return true;
        }
    }
}
