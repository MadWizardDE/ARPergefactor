using MadWizard.ARPergefactor.Config;
using MadWizard.ARPergefactor.Neighborhood;
using MadWizard.ARPergefactor.Neighborhood.Filter;
using MadWizard.ARPergefactor.Request;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using PacketDotNet;
using System.Net;

namespace MadWizard.ARPergefactor.Trigger
{
    internal class WakeOnARP(KnockerUp knocker) : IWakeTrigger
    {
        public required Network Network { private get; init; }

        bool IWakeTrigger.Handle(EthernetPacket packet)
        {
            if (packet.Type == EthernetType.Arp && packet.PayloadPacket is ArpPacket arp)
            {
                if (arp.Operation != ArpOperation.Request)
                    return false;
                if (arp.IsGratuitous() || arp.IsProbe() || arp.IsAnnouncement())
                    return false;

                if (Network.IsImpersonating(arp.TargetProtocolAddress))
                    return false; // already impersonating, so there is no need to trigger

                if (Network.FindWakeHostByAddress(arp.TargetProtocolAddress) is NetworkHost host)
                {
                    knocker.MakeHostAvailable(host, packet);
                }
            }

            return false;
        }
    }
}
