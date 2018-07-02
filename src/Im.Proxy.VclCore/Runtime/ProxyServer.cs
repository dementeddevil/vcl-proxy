using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Im.Proxy.VclCore.Model;
using SuperSocket.Common;
using SuperSocket.SocketBase;
using SuperSocket.SocketBase.Command;
using SuperSocket.SocketBase.Protocol;

namespace Im.Proxy.VclCore.Runtime
{
    public class ProxyServer : AppServer<ProxySession>
    {
        public ProxyServer()
        {
        }

    }

    public class ProxyHttpRequestInfo : IRequestInfo<VclContext>
    {
        /// <summary>
        /// Gets the key of this request.
        /// </summary>
        public string Key => Body.Request.RequestId;

        /// <summary>
        /// Gets the body of this request.
        /// </summary>
        public VclContext Body { get; } = new VclContext();
    }

    public class ProxyHttpRequestFilter : IReceiveFilter<ProxyHttpRequestInfo>
    {
        private static readonly byte[] HAProxyProtocolV1HeaderPrefix =
            { 0x50, 0x52, 0x4F, 0x58, 0x59 };
        private static readonly byte[] HAProxyProtocolV2HeaderPrefix =
            { 0x0D, 0x0A, 0x0D, 0x0A, 0x00 };

        private static readonly byte[] HAProxyProtocolV2HeaderRemainder =
            { 0x0D, 0x0A, 0x51, 0x55, 0x49, 0x54, 0x0A };

        private static readonly byte[] HAProxyProtocolV1HeaderTerminator =
            { 0x0D, 0x0A };

        private const int MaxProtocolDetectLength = 5;
        private const int MaxBufferSize = 256;
        private byte[] _inputBuffer = new byte[MaxBufferSize];
        private int _lastByteIndex;

        private ProxyHttpRequestInfo _requestInfo = new ProxyHttpRequestInfo();
        private ProxyFilterState _currentState;

        private enum ProxyFilterStatus
        {
            VersionDetect,
            ReadProtocolV1,
            ReadProtocolV2,
            Done
        }

        private static class FilterStateFactory
        {
            private static readonly IDictionary<ProxyFilterStatus, ProxyFilterState> States =
                new Dictionary<ProxyFilterStatus, ProxyFilterState>
                {
                    { ProxyFilterStatus.VersionDetect, new DetectProtocolVersionProxyFilterState() },
                    { ProxyFilterStatus.ReadProtocolV1, new ReadProtocolV1ProxyFilterState() },
                    { ProxyFilterStatus.ReadProtocolV2, new ReadProtocolV2ProxyFilterState() },
                    { ProxyFilterStatus.Done, new DoneProxyFilterState() }
                };

            public static ProxyFilterState Get(ProxyFilterStatus status)
            {
                return States[status];
            }
        }

        private abstract class ProxyFilterState
        {
            public abstract ProxyHttpRequestInfo Filter(
                ProxyHttpRequestFilter instance,
                byte[] readBuffer,
                int offset,
                int length,
                bool toBeCopied,
                out int rest);

            protected void SwitchState(ProxyHttpRequestFilter instance, ProxyFilterStatus newStatus)
            {
                instance._currentState = FilterStateFactory.Get(newStatus);
            }
        }

        private class DetectProtocolVersionProxyFilterState : ProxyFilterState
        {
            public override ProxyHttpRequestInfo Filter(ProxyHttpRequestFilter instance, byte[] readBuffer, int offset, int length, bool toBeCopied, out int rest)
            {
                // Take as many bytes as we need to determine the proxy version
                int maxBytes = Math.Min(length, MaxProtocolDetectLength - instance._lastByteIndex);
                Array.Copy(readBuffer, offset, instance._inputBuffer, instance._lastByteIndex, maxBytes);
                instance._lastByteIndex += maxBytes;

                // If we have enough data to determine new state...
                if (length >= MaxProtocolDetectLength)
                {
                    // Input MUST start with one of our detection arrays
                    if (Array.IndexOf(instance._inputBuffer, HAProxyProtocolV1HeaderPrefix, 0, MaxProtocolDetectLength) == 0)
                    {
                        SwitchState(instance, ProxyFilterStatus.ReadProtocolV1);
                    }
                    else if (Array.IndexOf(instance._inputBuffer, HAProxyProtocolV2HeaderPrefix, 0, MaxProtocolDetectLength) == 0)
                    {
                        SwitchState(instance, ProxyFilterStatus.ReadProtocolV2);
                    }
                    else
                    {
                        SwitchState(instance, ProxyFilterStatus.Done);
                    }
                }

                // Update number of bytes we've used
                rest = length - maxBytes;
                return null;
            }
        }

        private class ReadProtocolV1ProxyFilterState : ProxyFilterState
        {
            public override ProxyHttpRequestInfo Filter(ProxyHttpRequestFilter instance, byte[] readBuffer, int offset, int length, bool toBeCopied, out int rest)
            {
                // Take as many bytes as we need to determine the proxy version
                int maxBytes = Math.Min(length, MaxBufferSize - instance._lastByteIndex);
                var hasCompleteHeader = false;
                for (var index = 0; index < maxBytes; ++index)
                {
                    instance._inputBuffer[instance._lastByteIndex] = readBuffer[offset + index];
                    ++instance._lastByteIndex;
                    --length;

                    if (Array.IndexOf(instance._inputBuffer, HAProxyProtocolV1HeaderTerminator, 0, instance._lastByteIndex) != -1)
                    {
                        hasCompleteHeader = true;
                        break;
                    }
                }

                if (hasCompleteHeader)
                {
                    // Convert entire input buffer into ASCII and parse
                    var proxyHeader = Encoding.ASCII.GetString(instance._inputBuffer, 0, instance._lastByteIndex);

                    // We expect the form
                    // PROXY<SPC>protocol-family<SPC>source-address<SPC>destination-address<SPC>source-port<SPC>destination-port<CR><LF>
                    var parts = proxyHeader.Split(' ');
                    if (parts.Length == 6 && parts[5].EndsWith("\r\n") &&
                        (parts[1].Equals("TCP4") || parts[1].Equals("TCP6")))
                    {
                        if (IPAddress.TryParse(parts[2], out var sourceAddress) &&
                            IPAddress.TryParse(parts[3], out var destinationAddress) &&
                            int.TryParse(parts[4], out var sourcePort) &&
                            int.TryParse(parts[5].TrimEnd('\r', '\n'), out var destinationPort))
                        {
                            // We have successfully parsed source and destination information
                            //  so update the context client information
                            instance._requestInfo.Body.Client.Ip = sourceAddress;
                            instance._requestInfo.Body.Client.Port = sourcePort;
                            instance._requestInfo.Body.Server.Ip = destinationAddress;
                            instance._requestInfo.Body.Server.Port = destinationPort;
                        }
                    }

                    // Reset the index tracker so done state functions correctly
                    instance._lastByteIndex = 0;
                    SwitchState(instance, ProxyFilterStatus.Done);
                }
                else if (instance._lastByteIndex >= MaxBufferSize)
                {
                    // This is an error state - we have failed to parse the input and are still searching for CR\LF
                    //  assume input is junk....
                    SwitchState(instance, ProxyFilterStatus.Done);
                }

                // Update number of bytes we've used
                rest = length;
                return null;
            }
        }

        private class ReadProtocolV2ProxyFilterState : ProxyFilterState
        {
            public override ProxyHttpRequestInfo Filter(ProxyHttpRequestFilter instance, byte[] readBuffer, int offset, int length, bool toBeCopied, out int rest)
            {
                throw new NotImplementedException();
            }
        }

        private class DoneProxyFilterState : ProxyFilterState
        {
            public override ProxyHttpRequestInfo Filter(
                ProxyHttpRequestFilter instance,
                byte[] readBuffer,
                int offset,
                int length,
                bool toBeCopied,
                out int rest)
            {
                // If the last index is zero then we read the version without any extra bytes
                if (instance._lastByteIndex == 0)
                {
                    // We can return the object (making sure we leave the buffer untouched)
                    rest = length;
                    return instance._requestInfo;
                }

                // In all other cases we need to read until the next newline
                //  as this represents the first line of the next HTTP header
            }
        }

        public ProxyHttpRequestFilter(IReceiveFilter<ProxyHttpRequestInfo> nextFilter = null)
        {
            NextReceiveFilter = nextFilter;
        }

        public int LeftBufferSize { get; private set; }

        public IReceiveFilter<ProxyHttpRequestInfo> NextReceiveFilter { get; }

        public FilterState State { get; private set; }

        public ProxyHttpRequestInfo Filter(byte[] readBuffer, int offset, int length, bool toBeCopied, out int rest)
        {
            return _currentState.Filter(this, readBuffer, offset, length, toBeCopied, out rest);
        }

        public void Reset()
        {
            throw new NotImplementedException();
        }
    }

    public class ProxySession : AppSession<ProxySession, StringRequestInfo>
    {
        public ProxySession()
        {
        }
    }
}
