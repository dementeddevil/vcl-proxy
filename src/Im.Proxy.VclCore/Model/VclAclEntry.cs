using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace Im.Proxy.VclCore.Model
{
    public abstract class VclAclEntry
    {
        private class VclAclHostNameEntry : VclAclEntry
        {
            private readonly bool _ignorable;

            public VclAclHostNameEntry(string host, bool ignorable)
            {
                _ignorable = ignorable;

                Name = host;
            }

            public override string Name { get; }

            public override bool IsMatch(IPAddress client)
            {
                // Hopefully this method caches... (we may need to wrap this call)
                var addresses = Dns.GetHostAddresses(Name);

                // If lookup returns zero entries...
                if (addresses.Length == 0)
                {
                    // Non-ignorable entries match
                    // Ignorable entries don't match
                    return !_ignorable;
                }

                return addresses.Any(address => address == client);
            }
        }

        private class VclAclIpAddressEntry : VclAclEntry
        {
            private readonly IPAddress _address;

            public VclAclIpAddressEntry(string ipAddress)
            {
                _address = IPAddress.Parse(ipAddress);
                Name = ipAddress;
            }

            public override string Name { get; }

            public override bool IsMatch(IPAddress client)
            {
                return _address == client;
            }
        }

        private class VclAclSubnetEntry : VclAclEntry
        {
            private IPAddress _address;
            private int _mask;

            public VclAclSubnetEntry(string ipAddressAndMask)
            {
                var parts = ipAddressAndMask.Split('/');
                if (parts.Length != 2)
                {
                    throw new ArgumentException("Invalid subnet string");
                }

                _address = IPAddress.Parse(parts[0]);
                var mask = Int32.Parse(parts[1]);
                for (int index = 0; index < mask; ++index)
                {
                    _mask |= 1 << (31 - index);
                }

                if (_address.AddressFamily != AddressFamily.InterNetwork)
                {
                    throw new ArgumentException("Subnet logic only supports IPV4");
                }

                if (_mask < 1 || _mask > 32)
                {
                    throw new ArgumentException("Invalid subnet string");
                }

                Name = ipAddressAndMask;
            }

            public override string Name { get; }

            public override bool IsMatch(IPAddress client)
            {
#pragma warning disable CS0618 // Type or member is obsolete
                return ((_address.Address & _mask) ^ (client.Address & _mask)) == 0;
#pragma warning restore CS0618 // Type or member is obsolete
            }
        }

        public abstract string Name { get; }

        public abstract bool IsMatch(IPAddress client);

        public static VclAclEntry FromHostName(string host, bool ignorable)
        {
            return new VclAclHostNameEntry(host, ignorable);
        }

        public static VclAclEntry FromAddress(string ipAddress)
        {
            return new VclAclIpAddressEntry(ipAddress);
        }

        public static VclAclEntry FromSubnet(string ipAddressAndMask)
        {
            return new VclAclSubnetEntry(ipAddressAndMask);
        }
    }
}