using MadWizard.ARPergefactor.Config;
using MadWizard.ARPergefactor.Neighborhood;
using Microsoft.Extensions.Options;
using PacketDotNet;
using System.Diagnostics.Metrics;

namespace MadWizard.ARPergefactor.Request.Filter
{
    internal class RouterFilter : IWakeRequestFilter
    {
        public required Network Network { private get; init; }

        public required WakeRequest Request { private get; init; }

        async Task<bool?> IWakeRequestFilter.ShouldFilterPacket(EthernetPacket packet)
        {
            foreach (var router in Network.OfType<NetworkRouter>())
            {
                if (router.HasAddress(Request.SourcePhysicalAddress, Request.SourceIPAddress))
                {
                    if (router.Options.AllowWakeOnLAN)
                    {
                        // WakeOnLAN packets by routers are unusual and shall therefore not be filtered
                        if (Request.TriggerPacket.Extract<WakeOnLanPacket>() is not null)
                            return false;
                    }

                    return true;
                }
            }

            return false;
        }
    }
}
