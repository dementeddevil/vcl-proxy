using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Im.Proxy.VclCore.Model;
using Microsoft.AspNetCore.Http;
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
        Abandon,
        Fetch,
        Response,
        Retry,
        Deliver,
        Error,
        Done
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
            private static readonly Dictionary<string, MethodInfo> MethodLookup =
                new Dictionary<string, MethodInfo>()
                {
                    { "vcl_init", typeof(VclHandler).GetMethod(nameof(VclInit)) },
                    { "vcl_recv", typeof(VclHandler).GetMethod(nameof(VclReceive)) },
                    { "vcl_hash", typeof(VclHandler).GetMethod(nameof(VclHash)) },
                    { "vcl_pipe", typeof(VclHandler).GetMethod(nameof(VclPipe)) },
                    { "vcl_pass", typeof(VclHandler).GetMethod(nameof(VclPass)) },
                    { "vcl_hit", typeof(VclHandler).GetMethod(nameof(VclHit)) },
                    { "vcl_miss", typeof(VclHandler).GetMethod(nameof(VclMiss)) },
                    { "vcl_fetch", typeof(VclHandler).GetMethod(nameof(VclFetch)) },
                    { "vcl_deliver", typeof(VclHandler).GetMethod(nameof(VclDeliver)) },
                    { "vcl_purge", typeof(VclHandler).GetMethod(nameof(VclPurge)) },
                    { "vcl_synth", typeof(VclHandler).GetMethod(nameof(VclSynth)) },
                    { "vcl_error", typeof(VclHandler).GetMethod(nameof(VclError)) },
                    { "vcl_backend_fetch", typeof(VclHandler).GetMethod(nameof(VclBackendFetch)) },
                    { "vcl_backend_response", typeof(VclHandler).GetMethod(nameof(VclBackendResponse)) },
                    { "vcl_backend_error", typeof(VclHandler).GetMethod(nameof(VclBackendError)) },
                };

            public static MethodInfo GetSystemMethodInfo(string vclSubroutineName)
            {
                return MethodLookup.TryGetValue(vclSubroutineName, out var mi) ? mi : null;
            }
        }

        #region Frontend State Machine
        private abstract class VclFrontendHandlerState
        {
            public abstract VclAction State { get; }

            protected abstract VclAction[] ValidTransitionStates { get; }

            public abstract void Execute(VclHandler handler, RequestContext requestContext, VclContext vclContext);

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

            public override void Execute(VclHandler handler, RequestContext requestContext, VclContext vclContext)
            {
                ++handler._frontendAttempt;
                if (handler._frontendAttempt >= handler._maxFrontendRetries)
                {
                    SwitchState(handler, VclAction.Synth);
                }
                else
                {
                    SwitchState(handler, VclAction.Receive);
                }
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

            public override async void Execute(VclHandler handler, RequestContext requestContext, VclContext vclContext)
            {
                var nextState = handler.VclReceive(vclContext);
                SwitchState(handler, nextState);
            }
        }

        private class VclHashFrontendHandlerState : VclFrontendHandlerState
        {
            public override VclAction State => VclAction.Pipe;

            protected override VclAction[] ValidTransitionStates =>
                new[]
                {
                    VclAction.Lookup
                };

            public override async void Execute(VclHandler handler, RequestContext requestContext, VclContext vclContext)
            {
                var nextState = handler.VclHash(vclContext);
                SwitchState(handler, nextState);
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

            public override void Execute(VclHandler handler, RequestContext requestContext, VclContext vclContext)
            {
                // TODO: Attempt to do a lookup given the hash code
                // we will either end up with a cache hit, miss or something else

                // For now simply skip trying to do any cache lookup whatsoever
                //  and switch to Miss state
                SwitchState(handler, VclAction.Miss);
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

            public override void Execute(VclHandler handler, RequestContext requestContext, VclContext vclContext)
            {
                var nextState = handler.VclHit(vclContext);
                SwitchState(handler, nextState);
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

            public override void Execute(VclHandler handler, RequestContext requestContext, VclContext vclContext)
            {
                var nextState = handler.VclMiss(vclContext);
                SwitchState(handler, nextState);
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

            public override void Execute(VclHandler handler, RequestContext requestContext, VclContext vclContext)
            {
                var nextState = handler.VclPass(vclContext);
                SwitchState(handler, nextState);
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

            public override void Execute(VclHandler handler, RequestContext requestContext, VclContext vclContext)
            {
                var nextState = handler.VclPipe(vclContext);
                SwitchState(handler, nextState);
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

            public override void Execute(VclHandler handler, RequestContext requestContext, VclContext vclContext)
            {
                var nextState = handler.VclPurge(vclContext);
                SwitchState(handler, nextState);
            }
        }

        private class VclDeliverFrontendHandlerState : VclFrontendHandlerState
        {
            public override VclAction State => VclAction.Pipe;

            protected override VclAction[] ValidTransitionStates =>
                new[]
                {
                    VclAction.Synth,
                    VclAction.Restart,
                    VclAction.DeliverContent
                };

            public override void Execute(VclHandler handler, RequestContext requestContext, VclContext vclContext)
            {
                var nextState = handler.VclDeliver(vclContext);
                SwitchState(handler, nextState);
            }
        }

        private class VclSynthFrontendHandlerState : VclFrontendHandlerState
        {
            public override VclAction State => VclAction.Pipe;

            protected override VclAction[] ValidTransitionStates =>
                new[]
                {
                    VclAction.DeliverContent,
                    VclAction.Restart
                };

            public override void Execute(VclHandler handler, RequestContext requestContext, VclContext vclContext)
            {
                var nextState = handler.VclPurge(vclContext);
                SwitchState(handler, nextState);
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

            public override async void Execute(VclHandler handler, RequestContext requestContext, VclContext vclContext)
            {

                SwitchState(handler, VclAction.Done);
            }
        }

        private class VclDoneFrontendHandlerState : VclFrontendHandlerState
        {
            public override VclAction State => VclAction.Done;

            protected override VclAction[] ValidTransitionStates => new VclAction[0];

            public override void Execute(VclHandler handler, RequestContext requestContext, VclContext vclContext)
            {
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

            public abstract void Execute(VclHandler handler, VclContext vclContext);

            protected virtual void SwitchState(VclHandler handler, VclAction action)
            {
                // By default we allow the switch to the new state
                // Derived classes may apply restriction to transitions allowed
                handler._currentFrontendState = VclFrontendHandlerStateFactory.Get(action);
            }
        }

        private class VclAbandonBackendHandlerState : VclBackendHandlerState
        {
            public override VclBackendAction State => VclBackendAction.Abandon;

            protected override VclBackendAction[] ValidTransitionStates =>
                new[]
                {
                    VclBackendAction.Done,
                };

            public override void Execute(VclHandler handler, VclContext vclContext)
            {

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

            public override void Execute(VclHandler handler, VclContext vclContext)
            {

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

            public override void Execute(VclHandler handler, VclContext vclContext)
            {

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

            public override void Execute(VclHandler handler, VclContext vclContext)
            {

            }
        }

        private class VclDeliverBackendHandlerState : VclBackendHandlerState
        {
            public override VclBackendAction State => VclBackendAction.Deliver;

            protected override VclBackendAction[] ValidTransitionStates =>
                new[]
                {
                    VclBackendAction.Done,
                };

            public override void Execute(VclHandler handler, VclContext vclContext)
            {

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

            public override void Execute(VclHandler handler, VclContext vclContext)
            {

            }
        }

        private class VclDoneBackendHandlerState : VclBackendHandlerState
        {
            public override VclBackendAction State => VclBackendAction.Done;

            protected override VclBackendAction[] ValidTransitionStates => new VclBackendAction[0];

            public override void Execute(VclHandler handler, VclContext vclContext)
            {

            }
        }

        private static class VclBackendHandlerStateFactory
        {
            private static readonly Dictionary<VclBackendAction, VclBackendHandlerState> KnownStates =
                new Dictionary<VclBackendAction, VclBackendHandlerState>
                {
                    { VclBackendAction.Abandon, new VclAbandonBackendHandlerState() },
                    { VclBackendAction.Fetch, new VclFetchBackendHandlerState() },
                    { VclBackendAction.Response, new VclResponseBackendHandlerState() },
                    { VclBackendAction.Retry, new VclRetryBackendHandlerState() },
                    { VclBackendAction.Deliver, new VclDeliverBackendHandlerState() },
                    { VclBackendAction.Error, new VclErrorBackendHandlerState() },
                    { VclBackendAction.Done, new VclDoneBackendHandlerState() }
                };

            public static VclBackendHandlerState Get(VclBackendAction action)
            {
                return KnownStates[action];
            }
        }

        #endregion

        private VclFrontendHandlerState _currentFrontendState = VclFrontendHandlerStateFactory.Get(VclAction.Restart);
        private VclBackendHandlerState _currentBackendState = VclBackendHandlerStateFactory.Get(VclBackendAction.Fetch);

        private int _frontendAttempt;
        private int _maxFrontendRetries = 5;

        private int _backendAttempt;
        private int _maxBackendRetries = 3;
        private int _defaultTtl = 120;

        public async Task ProcessFrontendRequest(RequestContext requestContext)
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
                // Reset the context if we are in the restart state
                if (_currentFrontendState.State == VclAction.Restart)
                {
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
                    vclContext.Request.Restarts++;
                    foreach (var header in requestContext.Request.Headers)
                    {
                        vclContext.Request.Headers.Add(header.Key, header.Value.ToString());
                    }
                }

                // Execute state object
                _currentFrontendState.Execute(this, requestContext, vclContext);
            }
        }

        public virtual async void ProcessBackendFetch(VclContext context)
        {
            // TODO: Setup BE request parameters
            context.BackendRequest = new VclBackendRequest();

            // Allow hooks to issue modify backend fetch parameters
            await VclBackendFetch(context);

            // TODO: Issue request to backend
            var httpClient = new HttpClient();
            var backendRequest = new HttpRequestMessage();
            backendRequest.Method = new HttpMethod(context.BackendRequest.Method);
            backendRequest.RequestUri = new Uri(context.BackendRequest.Uri);
            foreach (var entry in context.BackendRequest.Headers)
            {
                backendRequest.Headers.Add(entry.Key, entry.Value);
            }

            // Get raw response from backend
            var backendResponse = await httpClient
                .SendAsync(backendRequest)
                .ConfigureAwait(false);


            // TODO: Setup VCL backend response
            context.BackendResponse = new VclBackendResponse();

            // Setup response TTL value
            if (backendResponse.Headers.CacheControl.SharedMaxAge != null)
            {
                context.BackendResponse.Ttl = (int)backendResponse.Headers.CacheControl.SharedMaxAge.Value.TotalSeconds;
            }
            else if (backendResponse.Headers.CacheControl.MaxAge != null)
            {
                context.BackendResponse.Ttl = (int)backendResponse.Headers.CacheControl.MaxAge.Value.TotalSeconds;
            }
            else if (backendResponse.Headers.TryGetValues("Expires", out var expiryValues))
            {
                var expiryDate = DateTime.Parse(expiryValues.First());
                context.BackendResponse.Ttl = (int)(expiryDate - DateTime.UtcNow).TotalSeconds;
            }
            else
            {
                context.BackendResponse.Ttl = _defaultTtl;
            }

            // Pass control to vcl_backend_response method
            await VclBackendResponse(context).ConfigureAwait(false);

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


        protected virtual Task<VclBackendAction> VclBackendFetch(VclContext context)
        {
            return VclBackendAction.Fetch);
        }

        protected virtual Task VclBackendResponse(VclContext context)
        {
            return Task.CompletedTask;
        }

        protected virtual Task VclBackendError(VclContext context)
        {
            return Task.CompletedTask;
        }
    }


}
