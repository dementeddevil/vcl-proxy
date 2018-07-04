using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Net.Http.Server;

namespace Im.Proxy.VclCore.Runtime
{
    public class WebListenerProxy : IDisposable
    {
        private readonly WebListener _webListener = new WebListener();
        private readonly IList<Task> _handlerThreads = new List<Task>();
        private readonly CancellationTokenSource _shutdown = new CancellationTokenSource();
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

        public void Stop()
        {
            _shutdown.Cancel();
        }

        public void Dispose()
        {
            _shutdown.Cancel();
            Task.WaitAll(_handlerThreads.ToArray());
            foreach (var task in _handlerThreads)
            {
                task.Dispose();
            }
            _handlerThreads.Clear();
            _webListener.Dispose();
            _shutdown.Dispose();
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

                // Pass request to the handler for execution
                await handler
                    .ProcessFrontendRequestAsync(requestContext)
                    .ConfigureAwait(false);
            }

            handler?.VclTerm(null);
        }
    }
}
