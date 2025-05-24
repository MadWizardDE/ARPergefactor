using MadWizard.ARPergefactor.Config;
using MadWizard.ARPergefactor.Neighborhood;
using MadWizard.ARPergefactor.Neighborhood.Filter;
using MadWizard.ARPergefactor.Request;
using Microsoft.Extensions.Options;
using PacketDotNet;
using SharpPcap;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.ARPergefactor.Trigger
{
    internal class WakeOnNDP(KnockerUp knocker) : IWakeTrigger
    {
        public required Network Network { private get; init; }

        bool IWakeTrigger.Handle(EthernetPacket packet)
        {

            if (packet.Type == EthernetType.IPv6 && packet.PayloadPacket is IPv6Packet ipv6)
                if (ipv6.Protocol == ProtocolType.IcmpV6 && ipv6.PayloadPacket is IcmpV6Packet icmpv6)
                    if (icmpv6.Type == IcmpV6Type.NeighborSolicitation && icmpv6.PayloadPacket is NdpNeighborSolicitationPacket sol)
                    {
                        if (ipv6.SourceAddress.Equals(IPAddress.IPv6Any))
                            return false; // don't react to DAD

                        if (Network.IsImpersonating(sol.TargetAddress))
                            return false; // already impersonating, so there is no need to trigger

                        if (Network.FindWakeHostByAddress(sol.TargetAddress) is NetworkHost host)
                        {
                            knocker.MakeHostAvailable(host, packet);

                            return true;
                        }
                    }

            return false;
        }
    }
}
