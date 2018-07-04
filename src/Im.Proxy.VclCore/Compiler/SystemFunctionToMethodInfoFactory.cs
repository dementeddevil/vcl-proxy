using System.Collections.Generic;
using Im.Proxy.VclCore.Model;

namespace Im.Proxy.VclCore.Compiler
{
    public static class SystemFunctionToMethodInfoFactory
    {
        private static readonly Dictionary<string, string> MethodLookup =
            new Dictionary<string, string>()
            {
                { "vcl_init", nameof(VclHandler.VclInit) },
                { "vcl_recv", nameof(VclHandler.VclReceive) },
                { "vcl_hash", nameof(VclHandler.VclHash) },
                { "vcl_pipe", nameof(VclHandler.VclPipe) },
                { "vcl_pass", nameof(VclHandler.VclPass) },
                { "vcl_hit", nameof(VclHandler.VclHit) },
                { "vcl_miss", nameof(VclHandler.VclMiss) },
                { "vcl_fetch", nameof(VclHandler.VclFetch) },
                { "vcl_deliver", nameof(VclHandler.VclDeliver) },
                { "vcl_purge", nameof(VclHandler.VclPurge) },
                { "vcl_synth", nameof(VclHandler.VclSynth) },
                { "vcl_error", nameof(VclHandler.VclError) },
                { "vcl_backend_fetch", nameof(VclHandler.VclBackendFetch) },
                { "vcl_backend_response", nameof(VclHandler.VclBackendResponse) },
                { "vcl_backend_error", nameof(VclHandler.VclBackendError) },
                { "vcl_term", nameof(VclHandler.VclTerm) }
            };

        public static string GetSystemMethodName(string vclSubroutineName)
        {
            return MethodLookup.TryGetValue(vclSubroutineName, out var mi) ? mi : null;
        }
    }
}