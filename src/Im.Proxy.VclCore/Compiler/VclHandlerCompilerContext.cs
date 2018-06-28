using System.Collections.Generic;
using Im.Proxy.VclCore.Model;

namespace Im.Proxy.VclCore.Compiler
{
    public class VclHandlerCompilerContext
    {
        public IList<VclBackend> Backends { get; } = new List<VclBackend>();

        public IList<VclProbe> Probes { get; } = new List<VclProbe>();

        //public IList<VclAcl> Acls { get; } = new List<VclAcl>();
    }
}