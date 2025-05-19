using MadWizard.ARPergefactor.Config;
using MadWizard.ARPergefactor.Neighborhood;
using Microsoft.Extensions.Options;
using PacketDotNet;
using System.Diagnostics.Metrics;
using System.Net.Sockets;

namespace MadWizard.ARPergefactor.Request.Filter
{
    internal class RouterFilter : IWakeRequestFilter
    {
        public required Network Network { private get; init; }

        public required WakeRequest Request { private get; init; }

        public bool NeedsIPUnicast => IsSentByRouter(Request.TriggerPacket);

        bool IWakeRequestFilter.ShouldFilterPacket(EthernetPacket packet, out bool foundMatch)
        {
            foundMatch = false;

            if (IsSentByRouter(Request.TriggerPacket))
                if (Request.TriggerPacket != packet)
                {
                    if (!IsSentByRouter(packet))
                    {
                        Request.TriggerPacket = packet;

                        return false;
                    }
                    else
                        return true;
                }

            return false;
        }

        private bool IsSentByRouter(EthernetPacket packet)
        {
            return Network.OfType<NetworkRouter>().Aggregate(false, (filter, router) => filter || router.HasAddress(packet.FindSourcePhysicalAddress(), packet.FindSourceIPAddress()));
        }
    }
}
