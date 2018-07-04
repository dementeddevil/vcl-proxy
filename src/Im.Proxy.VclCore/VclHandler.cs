using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Im.Proxy.VclCore.Model;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
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
        Error,
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
        #region Frontend State Machine
        private abstract class VclFrontendHandlerState
        {
            public abstract VclAction State { get; }

            protected abstract VclAction[] ValidTransitionStates { get; }

            public abstract Task ExecuteAsync(VclHandler handler, RequestContext requestContext, VclContext vclContext);

            protected virtual void SwitchState(VclHandler handler, VclAction action)
            {
                if (!ValidTransitionStates.Contains(action))
                {
                    throw new InvalidOperationException($"Invalid attempt to transition front-end from {State} to {action}.");
                }

                handler.Logger?.LogDebug($"Client state transition from {State} to {action}");
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
                vclContext.Request.Restarts++;
                if (vclContext.Request.Restarts >= handler.MaxFrontendRetries)
                {
                    vclContext.Response =
                        new VclResponse
                        {
                            StatusCode = 500,
                            StatusDescription = "IMProxy request failed"
                        };
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
                // Attempt to do a lookup given the hash code
                var result = handler.Cache.Get<VclObject>($"VclObject:{vclContext.Request.Hash}");
                if (result == null)
                {
                    SwitchState(handler, VclAction.Miss);
                }
                else
                {
                    vclContext.Object = result;
                    SwitchState(handler, VclAction.Hit);
                }
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

        private class VclFetchFrontendHandlerState : VclFrontendHandlerState
        {
            public override VclAction State => VclAction.Fetch;

            protected override VclAction[] ValidTransitionStates =>
                new[]
                {
                    VclAction.Deliver,
                    VclAction.Restart
                };

            public override async Task ExecuteAsync(VclHandler handler, RequestContext requestContext, VclContext vclContext)
            {
                // Instruct backend request to execute
                var result = await handler
                    .ProcessBackendFetchAsync(vclContext)
                    .ConfigureAwait(false);

                // Detect error state and force request restart
                if (result == VclBackendAction.Abandon)
                {
                    SwitchState(handler, VclAction.Restart);
                    return;
                }

                // If the result is cachable then do it now
                if (!vclContext.BackendResponse.Uncacheable)
                {

                }

                SwitchState(handler, VclAction.Deliver);
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
                // Copy information from object into response
                vclContext.Response =
                    new VclResponse
                    {
                        StatusCode = vclContext.Object.StatusCode,
                        StatusDescription = vclContext.Object.StatusDescription,
                    };

                foreach (var header in vclContext.Object.Headers)
                {
                    vclContext.Response.Headers.Add(header.Key, header.Value);
                }

                if (vclContext.Object.Body != null)
                {
                    vclContext.Response.CopyBodyFrom(vclContext.Object.Body);
                }

                // Get result of calling extended code
                var nextState = handler.VclDeliver(vclContext);

                // Translate official supported action into internal code
                if (nextState == VclAction.Deliver)
                {
                    nextState = VclAction.DeliverContent;
                }

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

        private class VclErrorFrontendHandlerState : VclFrontendHandlerState
        {
            public override VclAction State => VclAction.Error;

            protected override VclAction[] ValidTransitionStates =>
                new[]
                {
                    VclAction.Restart,
                    VclAction.DeliverContent
                };

            public override Task ExecuteAsync(VclHandler handler, RequestContext requestContext, VclContext vclContext)
            {
                // Get result of calling extended code
                var nextState = handler.VclError(vclContext);

                // Translate official supported action into internal code
                if (nextState == VclAction.Deliver)
                {
                    nextState = VclAction.DeliverContent;
                }

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
                    { VclAction.Fetch, new VclFetchFrontendHandlerState() },
                    { VclAction.Error, new VclErrorFrontendHandlerState() },
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
                if (!ValidTransitionStates.Contains(action))
                {
                    throw new InvalidOperationException($"Invalid attempt to transition back-end from {State} to {action}.");
                }

                handler.Logger?.LogDebug($"Backend state transition from {State} to {action}");
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
                if (++handler._backendAttempt >= handler.MaxBackendRetries)
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
                    vclContext.BackendResponse.Ttl = handler.DefaultTtl;
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
                if (result != VclBackendAction.Deliver)
                {
                    SwitchState(handler, result);
                }

                // Create an object from the response
                vclContext.Object =
                    new VclObject
                    {
                        StatusCode = vclContext.BackendResponse.StatusCode,
                        StatusDescription = vclContext.BackendResponse.StatusDescription,
                        DoEsiProcessing = vclContext.BackendResponse.DoEsiProcessing,
                        Uncacheable = vclContext.BackendResponse.Uncacheable,
                        Ttl = vclContext.BackendResponse.Ttl,
                    };
                foreach (var header in vclContext.BackendResponse.Headers)
                {
                    vclContext.Object.Headers.Add(header.Key, header.Value);
                }

                if (vclContext.BackendResponse.Body != null)
                {
                    vclContext.Object.CopyBodyFrom(vclContext.BackendResponse.Body);
                }

                // Handle caching the object if we are allowed to
                if (!vclContext.Object.Uncacheable)
                {
                    handler.Cache.Set(
                        $"VclObject:{vclContext.Request.Hash}",
                        vclContext.Object,
                        TimeSpan.FromSeconds(vclContext.Object.Ttl));
                }

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
                // Disconnect backend request/response objects (unless we are in pipe mode)
                vclContext.BackendRequest = null;
                vclContext.BackendResponse = null;

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

        private int _backendAttempt;

        public int MaxFrontendRetries { get; set; } = 5;

        public int MaxBackendRetries { get; set; } = 3;

        public int DefaultTtl { get; set; } = 120;

        public IMemoryCache Cache { get; set; }

        public ILogger Logger { get; set; }

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

        public virtual VclAction VclFetch(VclContext context)
        {
            return VclAction.Fetch;
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

        public virtual VclAction VclError(VclContext context)
        {
            return VclAction.Deliver;
        }

        public virtual void VclTerm(VclContext context)
        {
        }

        public virtual VclBackendAction VclBackendFetch(VclContext context)
        {
            return VclBackendAction.Fetch;
        }

        public virtual VclBackendAction VclBackendResponse(VclContext context)
        {
            return VclBackendAction.Deliver;
        }

        public virtual void VclBackendError(VclContext context)
        {
        }
    }
}
