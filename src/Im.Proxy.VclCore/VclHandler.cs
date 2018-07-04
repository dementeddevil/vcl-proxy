using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Im.Proxy.VclCore.Model;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Server;

namespace Im.Proxy.VclCore
{
    public enum VclAction
    {
        NoOp,
        Restart,
        Receive,
        Hash,
        Lookup,
        Busy,
        Purge,
        Pass,
        Pipe,
        Synth,
        Hit,
        Miss,
        HitForPass,
        Fetch,
        Deliver,
        DeliverContent,
        Done
    }

    public enum VclBackendAction
    {
        Retry,
        Fetch,
        Response,
        Error,
        Deliver,
        Abandon
    }

    /// <summary>
    ///  VclHandler 
    /// </summary>
    /// <remarks>
    /// This class is derived from and built up using expression trees during
    /// the compile phase. The resultant expression tree is then cached as a
    /// compiled binary handler for a VclHandler
    /// </remarks>
    public class VclHandler
    {
        private static class SystemFunctionToMethodInfoFactory
        {
            private static readonly Dictionary<string, string> MethodLookup =
                new Dictionary<string, string>()
                {
                    { "vcl_init", nameof(VclInit) },
                    { "vcl_recv", nameof(VclReceive) },
                    { "vcl_hash", nameof(VclHash) },
                    { "vcl_pipe", nameof(VclPipe) },
                    { "vcl_pass", nameof(VclPass) },
                    { "vcl_hit", nameof(VclHit) },
                    { "vcl_miss", nameof(VclMiss) },
                    { "vcl_fetch", nameof(VclFetch) },
                    { "vcl_deliver", nameof(VclDeliver) },
                    { "vcl_purge", nameof(VclPurge) },
                    { "vcl_synth", nameof(VclSynth) },
                    { "vcl_error", nameof(VclError) },
                    { "vcl_backend_fetch", nameof(VclBackendFetch) },
                    { "vcl_backend_response", nameof(VclBackendResponse) },
                    { "vcl_backend_error", nameof(VclBackendError) },
                    { "vcl_term", nameof(VclTerm) }
                };

            public static string GetSystemMethodInfo(string vclSubroutineName)
            {
                return MethodLookup.TryGetValue(vclSubroutineName, out var mi) ? mi : null;
            }
        }

        #region Frontend State Machine
        private abstract class VclFrontendHandlerState
        {
            public abstract VclAction State { get; }

            protected abstract VclAction[] ValidTransitionStates { get; }

            public abstract Task ExecuteAsync(VclHandler handler, RequestContext requestContext, VclContext vclContext);

            protected virtual void SwitchState(VclHandler handler, VclAction action)
            {
                // By default we allow the switch to the new state
                // Derived classes may apply restriction to transitions allowed
                handler._currentFrontendState = VclFrontendHandlerStateFactory.Get(action);
            }
        }

        private class VclRestartFrontendHandlerState : VclFrontendHandlerState
        {
            public override VclAction State => VclAction.Restart;

            protected override VclAction[] ValidTransitionStates =>
                new[]
                {
                    VclAction.Synth,
                    VclAction.Receive
                };

            public override Task ExecuteAsync(VclHandler handler, RequestContext requestContext, VclContext vclContext)
            {
                // Update restart count and abort with hard error if we exceed max-retries
                // TODO: Read max restarts value from configuration
                vclContext.Request.Restarts++;
                if (vclContext.Request.Restarts >= handler._maxFrontendRetries)
                {
                    vclContext.Response.StatusCode = 500;
                    vclContext.Response.StatusDescription = "IMProxy request failed";
                    SwitchState(handler, VclAction.Synth);
                    return Task.CompletedTask;
                }

                vclContext.Local.Ip = requestContext.Request.LocalIpAddress;
                vclContext.Remote.Ip = requestContext.Request.RemoteIpAddress;
                vclContext.Client.Ip = requestContext.Request.RemoteIpAddress;
                vclContext.Client.Port = requestContext.Request.RemotePort;
                vclContext.Server.HostName = Environment.MachineName;
                vclContext.Server.Identity = "foobar";
                vclContext.Server.Ip = requestContext.Request.LocalIpAddress;
                vclContext.Server.Port = requestContext.Request.LocalPort;
                vclContext.Request.Method = requestContext.Request.Method;
                vclContext.Request.Url = requestContext.Request.RawUrl;
                vclContext.Request.CanGzip =
                    requestContext.Request.Headers["Accept-Encoding"].Equals("gzip") ||
                    requestContext.Request.Headers["Accept-Encoding"].Equals("x-gzip");
                vclContext.Request.ProtocolVersion = requestContext.Request.ProtocolVersion.ToString(2);
                foreach (var header in requestContext.Request.Headers)
                {
                    vclContext.Request.Headers.Add(header.Key, header.Value.ToString());
                }

                SwitchState(handler, VclAction.Receive);
                return Task.CompletedTask;
            }
        }

        private class VclReceiveFrontendHandlerState : VclFrontendHandlerState
        {
            public override VclAction State => VclAction.Receive;

            protected override VclAction[] ValidTransitionStates =>
                new[]
                {
                    VclAction.Hash,
                    VclAction.Purge,
                    VclAction.Pass,
                    VclAction.Pipe,
                    VclAction.Synth
                };

            public override Task ExecuteAsync(VclHandler handler, RequestContext requestContext, VclContext vclContext)
            {
                // Allow VCL to tweak receive parameters
                var nextState = handler.VclReceive(vclContext);
                SwitchState(handler, nextState);
                return Task.CompletedTask;
            }
        }

        private class VclHashFrontendHandlerState : VclFrontendHandlerState
        {
            public override VclAction State => VclAction.Hash;

            protected override VclAction[] ValidTransitionStates =>
                new[]
                {
                    VclAction.Lookup
                };

            public override Task ExecuteAsync(VclHandler handler, RequestContext requestContext, VclContext vclContext)
            {
                var nextState = handler.VclHash(vclContext);
                SwitchState(handler, nextState);
                return Task.CompletedTask;
            }
        }

        private class VclLookupFrontendHandlerState : VclFrontendHandlerState
        {
            public override VclAction State => VclAction.Lookup;

            protected override VclAction[] ValidTransitionStates =>
                new[]
                {
                    VclAction.Hit,
                    VclAction.Miss,
                    VclAction.HitForPass,
                    VclAction.Busy
                };

            public override Task ExecuteAsync(VclHandler handler, RequestContext requestContext, VclContext vclContext)
            {
                // TODO: Attempt to do a lookup given the hash code
                // we will either end up with a cache hit, miss or something else

                // For now simply skip trying to do any cache lookup whatsoever
                //  and switch to Miss state
                SwitchState(handler, VclAction.Miss);
                return Task.CompletedTask;
            }
        }

        private class VclHitFrontendHandlerState : VclFrontendHandlerState
        {
            public override VclAction State => VclAction.Hit;

            protected override VclAction[] ValidTransitionStates =>
                new[]
                {
                    VclAction.Deliver,
                    VclAction.Miss,
                    VclAction.Restart,
                    VclAction.Synth,
                    VclAction.Pass
                };

            public override Task ExecuteAsync(VclHandler handler, RequestContext requestContext, VclContext vclContext)
            {
                var nextState = handler.VclHit(vclContext);
                SwitchState(handler, nextState);
                return Task.CompletedTask;
            }
        }

        private class VclMissFrontendHandlerState : VclFrontendHandlerState
        {
            public override VclAction State => VclAction.Miss;

            protected override VclAction[] ValidTransitionStates =>
                new[]
                {
                    VclAction.Fetch,
                    VclAction.Restart,
                    VclAction.Synth,
                    VclAction.Pass
                };

            public override Task ExecuteAsync(VclHandler handler, RequestContext requestContext, VclContext vclContext)
            {
                var nextState = handler.VclMiss(vclContext);
                SwitchState(handler, nextState);
                return Task.CompletedTask;
            }
        }

        private class VclPassFrontendHandlerState : VclFrontendHandlerState
        {
            public override VclAction State => VclAction.Pass;

            protected override VclAction[] ValidTransitionStates =>
                new[]
                {
                    VclAction.Synth,
                    VclAction.Fetch,
                    VclAction.Restart
                };

            public override Task ExecuteAsync(VclHandler handler, RequestContext requestContext, VclContext vclContext)
            {
                var nextState = handler.VclPass(vclContext);
                SwitchState(handler, nextState);
                return Task.CompletedTask;
            }
        }

        private class VclPipeFrontendHandlerState : VclFrontendHandlerState
        {
            public override VclAction State => VclAction.Pipe;

            protected override VclAction[] ValidTransitionStates =>
                new[]
                {
                    VclAction.Synth,
                    VclAction.Fetch
                };

            public override Task ExecuteAsync(VclHandler handler, RequestContext requestContext, VclContext vclContext)
            {
                var nextState = handler.VclPipe(vclContext);
                SwitchState(handler, nextState);
                return Task.CompletedTask;
            }
        }

        private class VclPurgeFrontendHandlerState : VclFrontendHandlerState
        {
            public override VclAction State => VclAction.Purge;

            protected override VclAction[] ValidTransitionStates =>
                new[]
                {
                    VclAction.Synth,
                    VclAction.Restart
                };

            public override Task ExecuteAsync(VclHandler handler, RequestContext requestContext, VclContext vclContext)
            {
                var nextState = handler.VclPurge(vclContext);
                SwitchState(handler, nextState);
                return Task.CompletedTask;
            }
        }

        private class VclDeliverFrontendHandlerState : VclFrontendHandlerState
        {
            public override VclAction State => VclAction.Deliver;

            protected override VclAction[] ValidTransitionStates =>
                new[]
                {
                    VclAction.Synth,
                    VclAction.Restart,
                    VclAction.DeliverContent
                };

            public override Task ExecuteAsync(VclHandler handler, RequestContext requestContext, VclContext vclContext)
            {
                var nextState = handler.VclDeliver(vclContext);
                SwitchState(handler, nextState);
                return Task.CompletedTask;
            }
        }

        private class VclSynthFrontendHandlerState : VclFrontendHandlerState
        {
            public override VclAction State => VclAction.Synth;

            protected override VclAction[] ValidTransitionStates =>
                new[]
                {
                    VclAction.DeliverContent,
                    VclAction.Restart
                };

            public override Task ExecuteAsync(VclHandler handler, RequestContext requestContext, VclContext vclContext)
            {
                var nextState = handler.VclSynth(vclContext);
                SwitchState(handler, nextState);
                return Task.CompletedTask;
            }
        }

        private class VclDeliverContentFrontendHandlerState : VclFrontendHandlerState
        {
            public override VclAction State => VclAction.DeliverContent;

            protected override VclAction[] ValidTransitionStates =>
                new[]
                {
                    VclAction.Done
                };

            public override Task ExecuteAsync(VclHandler handler, RequestContext requestContext, VclContext vclContext)
            {
                // Setup real response
                requestContext.Response.StatusCode = vclContext.Response.StatusCode;
                requestContext.Response.ReasonPhrase = vclContext.Response.StatusDescription;
                foreach (var header in vclContext.Response.Headers)
                {
                    requestContext.Response.Headers.Add(header.Key, new StringValues(header.Value));
                }

                // Copy content body if we have one
                vclContext.Response.Body?.CopyTo(requestContext.Response.Body);

                // TODO: Setup cache TTL value (this uses kernel caching in HTTP.SYS driver)
                //requestContext.Response.CacheTtl = TimeSpan.FromSeconds(vclContext.Response.)

                SwitchState(handler, VclAction.Done);
                return Task.CompletedTask;
            }
        }

        private class VclDoneFrontendHandlerState : VclFrontendHandlerState
        {
            public override VclAction State => VclAction.Done;

            protected override VclAction[] ValidTransitionStates => new VclAction[0];

            public override Task ExecuteAsync(VclHandler handler, RequestContext requestContext, VclContext vclContext)
            {
                return Task.CompletedTask;
            }
        }

        private static class VclFrontendHandlerStateFactory
        {
            private static readonly Dictionary<VclAction, VclFrontendHandlerState> KnownStates =
                new Dictionary<VclAction, VclFrontendHandlerState>
                {
                    { VclAction.Restart, new VclRestartFrontendHandlerState() },
                    { VclAction.Receive, new VclReceiveFrontendHandlerState() },
                    { VclAction.Hash, new VclHashFrontendHandlerState() },
                    { VclAction.Lookup, new VclLookupFrontendHandlerState() },
                    { VclAction.Hit, new VclHitFrontendHandlerState() },
                    { VclAction.Miss, new VclMissFrontendHandlerState() },
                    { VclAction.Pass, new VclPassFrontendHandlerState() },
                    { VclAction.HitForPass, new VclPassFrontendHandlerState() },
                    { VclAction.Pipe, new VclPipeFrontendHandlerState() },
                    { VclAction.Purge, new VclPurgeFrontendHandlerState() },
                    { VclAction.Deliver, new VclDeliverFrontendHandlerState() },
                    { VclAction.DeliverContent, new VclDeliverContentFrontendHandlerState() },
                    { VclAction.Synth, new VclSynthFrontendHandlerState() },
                    { VclAction.Done, new VclDoneFrontendHandlerState() }
                };

            public static VclFrontendHandlerState Get(VclAction action)
            {
                return KnownStates[action];
            }
        }
        #endregion

        #region Backend State Machine
        private abstract class VclBackendHandlerState
        {
            public abstract VclBackendAction State { get; }

            protected abstract VclBackendAction[] ValidTransitionStates { get; }

            public abstract Task ExecuteAsync(VclHandler handler, VclContext vclContext);

            protected virtual void SwitchState(VclHandler handler, VclBackendAction action)
            {
                // By default we allow the switch to the new state
                // Derived classes may apply restriction to transitions allowed
                handler._currentBackendState = VclBackendHandlerStateFactory.Get(action);
            }
        }

        private class VclRetryBackendHandlerState : VclBackendHandlerState
        {
            public override VclBackendAction State => VclBackendAction.Retry;

            protected override VclBackendAction[] ValidTransitionStates =>
                new[]
                {
                    VclBackendAction.Abandon,
                    VclBackendAction.Fetch
                };

            public override Task ExecuteAsync(VclHandler handler, VclContext vclContext)
            {
                // Handle case where we exceed number of backend retries
                if (++handler._backendAttempt >= handler._maxBackendRetries)
                {
                    SwitchState(handler, VclBackendAction.Abandon);
                    return Task.CompletedTask;
                }

                // Setup BE request parameters
                vclContext.BackendRequest =
                    new VclBackendRequest
                    {
                        Method = vclContext.Request.Method,
                        Uri = vclContext.Request.Url
                    };
                foreach (var entry in vclContext.Request.Headers)
                {
                    vclContext.BackendRequest.Headers.Add(entry.Key, entry.Value);
                }

                // TODO: Handle translation of certain request METHOD values
                // TODO: Handle filtering out certain HTTP headers
                // TODO: Handle adding/modifying certain HTTP headers

                // Enter fetch state
                SwitchState(handler, VclBackendAction.Fetch);
                return Task.CompletedTask;
            }
        }

        private class VclFetchBackendHandlerState : VclBackendHandlerState
        {
            public override VclBackendAction State => VclBackendAction.Fetch;

            protected override VclBackendAction[] ValidTransitionStates =>
                new[]
                {
                    VclBackendAction.Abandon,
                    VclBackendAction.Fetch
                };

            public override async Task ExecuteAsync(VclHandler handler, VclContext vclContext)
            {
                var result = handler.VclBackendFetch(vclContext);
                if (result != VclBackendAction.Fetch)
                {
                    SwitchState(handler, result);
                    return;
                }

                // Issue request to backend
                var httpClient = new HttpClient();
                var backendRequest =
                    new HttpRequestMessage
                    {
                        Method = new HttpMethod(vclContext.BackendRequest.Method),
                        RequestUri = new Uri(vclContext.BackendRequest.Uri)
                    };
                foreach (var entry in vclContext.BackendRequest.Headers)
                {
                    backendRequest.Headers.Add(entry.Key, entry.Value);
                }

                // Get raw response from backend
                var backendResponse = await httpClient
                    .SendAsync(backendRequest)
                    .ConfigureAwait(false);

                // Setup VCL backend response
                vclContext.BackendResponse =
                    new VclBackendResponse
                    {
                        StatusCode = (int)backendResponse.StatusCode,
                        StatusDescription = backendResponse.ReasonPhrase
                    };
                foreach (var item in backendResponse.Headers)
                {
                    vclContext.BackendResponse.Headers.Add(item.Key, string.Join(",", item.Value));
                }

                // Detect backend error
                if (!backendResponse.IsSuccessStatusCode)
                {
                    SwitchState(handler, VclBackendAction.Error);
                    return;
                }

                // If we have a content body in the response then copy it now
                // TODO: May need to steal VirtualStream code that uses MemoryStream until content
                //  exceeds certain size where it switches to a temporary file
                if (backendResponse.Content != null)
                {
                    using (var contentBodyStream = await backendResponse.Content.ReadAsStreamAsync())
                    {
                        vclContext.BackendResponse.CopyBodyFrom(contentBodyStream);
                    }
                }

                // Setup response TTL value
                if (backendResponse.Headers.CacheControl.SharedMaxAge != null)
                {
                    vclContext.BackendResponse.Ttl = (int)backendResponse.Headers.CacheControl.SharedMaxAge.Value.TotalSeconds;
                }
                else if (backendResponse.Headers.CacheControl.MaxAge != null)
                {
                    vclContext.BackendResponse.Ttl = (int)backendResponse.Headers.CacheControl.MaxAge.Value.TotalSeconds;
                }
                else if (backendResponse.Headers.TryGetValues("Expires", out var expiryValues))
                {
                    var expiryDate = DateTime.Parse(expiryValues.First());
                    vclContext.BackendResponse.Ttl = (int)(expiryDate - DateTime.UtcNow).TotalSeconds;
                }
                else
                {
                    vclContext.BackendResponse.Ttl = handler._defaultTtl;
                }

                // Switch state
                SwitchState(handler, VclBackendAction.Response);
            }
        }

        private class VclResponseBackendHandlerState : VclBackendHandlerState
        {
            public override VclBackendAction State => VclBackendAction.Response;

            protected override VclBackendAction[] ValidTransitionStates =>
                new[]
                {
                    VclBackendAction.Abandon,
                    VclBackendAction.Deliver,
                    VclBackendAction.Retry
                };

            public override Task ExecuteAsync(VclHandler handler, VclContext vclContext)
            {
                var result = handler.VclBackendResponse(vclContext);
                SwitchState(handler, result);
                return Task.CompletedTask;
            }
        }

        private class VclErrorBackendHandlerState : VclBackendHandlerState
        {
            public override VclBackendAction State => VclBackendAction.Error;

            protected override VclBackendAction[] ValidTransitionStates =>
                new[]
                {
                    VclBackendAction.Deliver,
                    VclBackendAction.Retry
                };

            public override Task ExecuteAsync(VclHandler handler, VclContext vclContext)
            {
                return Task.CompletedTask;
            }
        }

        private class VclDeliverBackendHandlerState : VclBackendHandlerState
        {
            public override VclBackendAction State => VclBackendAction.Deliver;

            protected override VclBackendAction[] ValidTransitionStates => new VclBackendAction[0];

            public override Task ExecuteAsync(VclHandler handler, VclContext vclContext)
            {
                return Task.CompletedTask;
            }
        }

        private class VclAbandonBackendHandlerState : VclBackendHandlerState
        {
            public override VclBackendAction State => VclBackendAction.Abandon;

            protected override VclBackendAction[] ValidTransitionStates => new VclBackendAction[0];

            public override Task ExecuteAsync(VclHandler handler, VclContext vclContext)
            {
                return Task.CompletedTask;
            }
        }

        private static class VclBackendHandlerStateFactory
        {
            private static readonly Dictionary<VclBackendAction, VclBackendHandlerState> KnownStates =
                new Dictionary<VclBackendAction, VclBackendHandlerState>
                {
                    { VclBackendAction.Retry, new VclRetryBackendHandlerState() },
                    { VclBackendAction.Fetch, new VclFetchBackendHandlerState() },
                    { VclBackendAction.Response, new VclResponseBackendHandlerState() },
                    { VclBackendAction.Error, new VclErrorBackendHandlerState() },
                    { VclBackendAction.Deliver, new VclDeliverBackendHandlerState() },
                    { VclBackendAction.Abandon, new VclAbandonBackendHandlerState() }
                };

            public static VclBackendHandlerState Get(VclBackendAction action)
            {
                return KnownStates[action];
            }
        }
        #endregion

        private VclFrontendHandlerState _currentFrontendState = VclFrontendHandlerStateFactory.Get(VclAction.Restart);
        private VclBackendHandlerState _currentBackendState = VclBackendHandlerStateFactory.Get(VclBackendAction.Fetch);

        private int _maxFrontendRetries = 5;

        private int _backendAttempt;
        private int _maxBackendRetries = 3;
        private int _defaultTtl = 120;

        public async Task ProcessFrontendRequestAsync(RequestContext requestContext)
        {
            var vclContext = new VclContext();
            vclContext.Request.Restarts = -1;
            if (requestContext.Request.HasEntityBody)
            {
                vclContext.Request.CopyBodyFrom(requestContext.Request.Body);
            }

            _currentFrontendState = VclFrontendHandlerStateFactory.Get(VclAction.Restart);
            while (_currentFrontendState.State != VclAction.Done)
            {
                await _currentFrontendState.ExecuteAsync(this, requestContext, vclContext).ConfigureAwait(false);
            }
        }

        public virtual async Task<VclBackendAction> ProcessBackendFetchAsync(VclContext context)
        {
            _backendAttempt = -1;
            _currentBackendState = VclBackendHandlerStateFactory.Get(VclBackendAction.Retry);
            while (_currentBackendState.State != VclBackendAction.Abandon &&
                   _currentBackendState.State != VclBackendAction.Deliver)
            {
                await _currentBackendState.ExecuteAsync(this, context).ConfigureAwait(false);
            }

            return _currentBackendState.State;
        }

        public virtual void VclInit(VclContext context)
        {
        }

        public virtual VclAction VclReceive(VclContext context)
        {
            return VclAction.Done;
        }

        public virtual VclAction VclHash(VclContext context)
        {
            return VclAction.Done;
        }

        public virtual VclAction VclPipe(VclContext context)
        {
            return VclAction.Fetch;
        }

        public virtual VclAction VclPass(VclContext context)
        {
            return VclAction.Done;
        }

        public virtual VclAction VclHit(VclContext context)
        {
            return VclAction.Done;
        }

        public virtual VclAction VclMiss(VclContext context)
        {
            return VclAction.Done;
        }

        public virtual void VclFetch(VclContext context)
        {
        }

        public virtual VclAction VclDeliver(VclContext context)
        {
            return VclAction.Done;
        }

        public virtual VclAction VclPurge(VclContext context)
        {
            return VclAction.Done;
        }

        public virtual VclAction VclSynth(VclContext context)
        {
            return VclAction.Deliver;
        }

        public virtual void VclError(VclContext context)
        {
        }

        public virtual void VclTerm(VclContext context)
        {
        }


        protected virtual VclBackendAction VclBackendFetch(VclContext context)
        {
            return VclBackendAction.Fetch;
        }

        protected virtual VclBackendAction VclBackendResponse(VclContext context)
        {
            return VclBackendAction.Done;
        }

        protected virtual void VclBackendError(VclContext context)
        {
        }
    }


}
