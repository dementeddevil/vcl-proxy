using System;

namespace Im.Proxy
{
    public interface IInboundRuleResolver
    {
        InboundRuleAction Get(Uri uri);
    }
}