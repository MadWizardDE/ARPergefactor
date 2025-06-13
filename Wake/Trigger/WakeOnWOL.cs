using MadWizard.ARPergefactor.Neighborhood;
using MadWizard.ARPergefactor.Wake.Methods;
using Microsoft.Extensions.Logging;
using PacketDotNet;

namespace MadWizard.ARPergefactor.Wake.Trigger
{
    internal class WakeOnWOL : IWakeTrigger
    {
        public required ILogger<WakeOnWOL> Logger { private get; init; }

        public required Network Network { private get; init; }

        NetworkWatchHost? IWakeTrigger.Examine(EthernetPacket packet, out bool skipFilters)
        {
            skipFilters = false;

            if (packet.IsWakeOnLAN(Network, out var wol) && !wol.IsUnmagicPacket(packet))
            {
                if (Network.Hosts[wol!.DestinationAddress] is NetworkWatchHost host)
                {
                    if (host is VirtualWatchHost virt)
                    {
                        skipFilters = virt.Rediretion == WakeOnLANRedirection.Always;

                        return host;
                    }
                    else
                    {
                        host.LastWake = DateTime.Now;

                        using (Logger.BeginHostScope(host))
                        {
                            _ = Logger.LogEvent(null, "Observed", host, packet);
                        }
                    }
                }
            }

            return null;
        }
    }
}
