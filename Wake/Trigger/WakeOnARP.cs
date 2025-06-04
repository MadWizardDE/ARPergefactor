using MadWizard.ARPergefactor.Config;
using MadWizard.ARPergefactor.Neighborhood;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using PacketDotNet;
using System.Net;

namespace MadWizard.ARPergefactor.Wake.Trigger
{
    internal class WakeOnARP : IWakeTrigger
    {
        public required Network Network { private get; init; }

        NetworkWatchHost? IWakeTrigger.Examine(EthernetPacket packet, out bool skipFilters)
        {
            skipFilters = false;

            if (packet.Type == EthernetType.Arp && packet.PayloadPacket is ArpPacket arp)
            {
                if (arp.Operation != ArpOperation.Request)
                    return null;
                if (arp.IsGratuitous() || arp.IsProbe() || arp.IsAnnouncement())
                    return null;

                if (Network.IsImpersonating(arp.TargetProtocolAddress))
                    return null; // already impersonating, so there is no need to trigger

                if (Network.Hosts[arp.TargetProtocolAddress] is NetworkWatchHost host)
                {
                    return host;
                }
            }

            return null;
        }
    }
}
