using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace System.Net.NetworkInformation
{
    internal static class IPAddressExt
    {
        public static IPAddress LinkLocalMulticast = IPAddress.Parse("ff02::1");
        public static IPAddress LinkLocalRouterMulticast = IPAddress.Parse("ff02::2");

        public static IPAddress DeriveIPv6SolicitedNodeMulticastAddress(this IPAddress ip)
        {
            byte[] bytes = ip.GetAddressBytes();

            switch (ip.AddressFamily)
            {
                case AddressFamily.InterNetworkV6:
                    if (ip.IsIPv6Multicast)
                        throw new ArgumentException("Is a multicast address.");

                    // IPv6 solicitated node multicast address
                    return IPAddress.Parse($"FF02::1:FF{bytes[13]:X2}:{bytes[14]:X2}{bytes[15]:X2}");

                default:
                    throw new NotSupportedException($"Unsupported address family: {ip.AddressFamily}");
            }
        }

        public static PhysicalAddress DeriveLayer2MulticastAddress(this IPAddress ip)
        {
            byte[] bytes = ip.GetAddressBytes();

            switch (ip.AddressFamily)
            {
                case AddressFamily.InterNetwork:
                    throw new NotImplementedException("IPv4 multicast address derivation not implemented.");

                case AddressFamily.InterNetworkV6:
                    if (!ip.IsIPv6Multicast)
                        throw new ArgumentException("Not a multicast address.");

                    // multicast MAC address
                    return PhysicalAddress.Parse($"33:33:{bytes[12]:X2}:{bytes[13]:X2}:{bytes[14]:X2}:{bytes[15]:X2}");

                default:
                    throw new NotSupportedException($"Unsupported address family: {ip.AddressFamily}");
            }
        }

        public static bool IsEmpty(this IPAddress ip)
        {
            return ip.Equals(IPAddress.Any);
        }

        public static bool IsAPIPA(this IPAddress ip)
        {
            byte[] bytes = ip.GetAddressBytes();

            // Check if it's IPv4 and starts with 169.254
            return bytes.Length == 4 && bytes[0] == 169 && bytes[1] == 254;
        }

        public static string ToFamilyName(this IPAddress ip)
        {
            return ip.AddressFamily == AddressFamily.InterNetworkV6 ? "IPv6" : "IPv4";
        }
    }
}
