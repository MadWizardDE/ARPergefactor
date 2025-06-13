using MadWizard.ARPergefactor.Neighborhood;
using PacketDotNet;
using System.Net;

namespace MadWizard.ARPergefactor.Wake.Trigger
{
    internal class WakeOnNDP : IWakeTrigger
    {
        public required Network Network { private get; init; }

        NetworkWatchHost? IWakeTrigger.Examine(EthernetPacket packet, out bool skipFilters)
        {
            skipFilters = false;
            if (packet.Type == EthernetType.IPv6 && packet.PayloadPacket is IPv6Packet ipv6)
                if (ipv6.Protocol == ProtocolType.IcmpV6 && ipv6.PayloadPacket is IcmpV6Packet icmpv6)
                    if (icmpv6.Type == IcmpV6Type.NeighborSolicitation && icmpv6.PayloadPacket is NdpNeighborSolicitationPacket sol)
                    {
                        if (ipv6.SourceAddress.Equals(IPAddress.IPv6Any))
                            return null; // don't react to DAD

                        if (Network.IsImpersonating(sol.TargetAddress))
                            return null; // already impersonating, so there is no need to trigger

                        if (Network.Hosts[sol.TargetAddress] is NetworkWatchHost host)
                        {
                            return host;
                        }
                    }

            return null;
        }
    }
}
