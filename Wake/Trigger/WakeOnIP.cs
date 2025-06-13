using MadWizard.ARPergefactor.Neighborhood;
using PacketDotNet;

namespace MadWizard.ARPergefactor.Wake.Trigger
{
    internal class WakeOnIP : IWakeTrigger
    {
        public required Network Network { private get; init; }

        NetworkWatchHost? IWakeTrigger.Examine(EthernetPacket packet, out bool skipFilters)
        {
            skipFilters = false;

            if ((packet.Type == EthernetType.IPv4 || packet.Type == EthernetType.IPv6) && packet.PayloadPacket is IPPacket ip)
                if (ip.Protocol == ProtocolType.Tcp || ip.Protocol == ProtocolType.Udp // all service requests
                    // PINGv4
                    || ip.Protocol == ProtocolType.Icmp && ip.PayloadPacket is IcmpV4Packet icmpv4
                        && icmpv4.TypeCode == IcmpV4TypeCode.EchoRequest
                    // PINGv6
                    || ip.Protocol == ProtocolType.IcmpV6 && ip.PayloadPacket is IcmpV6Packet icmpv6
                        && icmpv6.Type == IcmpV6Type.EchoRequest) 
                {
                    if (Network.Hosts[ip.DestinationAddress] is NetworkWatchHost host)
                    {
                        return host;
                    }
                }

            return null;
        }
    }
}