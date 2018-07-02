using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Im.Proxy.VclCore.Model;
using Microsoft.Net.Http.Server;

namespace Im.Proxy.VclCore.Runtime
{
    public class WebListenerProxy
    {
        private WebListener _webListener = new WebListener();
        private IList<Task> _handlerThreads = new List<Task>();
        private CancellationTokenSource _shutdown = new CancellationTokenSource();
        private readonly Type _vclHandlerType;

        public WebListenerProxy(string host, int hostPort, Type vclHandlerType)
        {
            _webListener.Settings.UrlPrefixes.Add($"http://+:{hostPort}/");
            _vclHandlerType = vclHandlerType;
        }

        public void Start()
        {
            _webListener.Start();

            for (int index = 0; index < 10; ++index)
            {
                _handlerThreads.Add(Task.Run(AcceptHandlerThread));
            }
        }

        private async Task AcceptHandlerThread()
        {
            VclHandler handler = null;
            while (_shutdown.IsCancellationRequested)
            {
                var requestContext = await _webListener
                    .AcceptAsync()
                    .ConfigureAwait(false);

                // Currently we do not support proxying websockets
                if (requestContext.IsWebSocketRequest)
                {
                    requestContext.Abort();
                    continue;
                }

                // Create VCL handler and initialise if we have not done so
                if (handler == null)
                {
                    handler = (VclHandler)Activator.CreateInstance(_vclHandlerType);
                    handler.VclInit(null);
                }

                // Enter processing loop
                // Create VCL context from inbound context

                // TODO: Read and buffer request data if any

                // Loop until we reach "done" state
                var vclContext = new VclContext();
                while (_currentFrontendState.State != VclAction.Done)
                {
                    // Reset the context if we are in the restart state
                    if (_currentFrontendState.State == VclAction.Restart)
                    {
                        vclContext.Client.Ip = context.Connection.RemoteIpAddress;
                        vclContext.Server.HostName = Environment.MachineName;
                        vclContext.Server.Identity = "foobar";
                        vclContext.Server.Ip = context.Connection.LocalIpAddress;
                        vclContext.Server.Port = context.Connection.LocalPort;
                        vclContext.Request.Method = context.Request.Method;
                        vclContext.Request.Url = uri.ToString();
                        foreach (var header in context.Request.Headers)
                        {
                            vclContext.Request.Headers.Add(header.Key, header.Value.ToString());
                        }
                    }

                    // Pass execution to current state
                    // TODO: We probably need to pass the original context so we can handle sending the reply
                    _currentFrontendState.Execute(this, context, vclContext);
                }

            }

            if (handler != null)
            {
                handler.VclTerm(null);
            }
        }
    }
}
