using Autofac.Core;
using MadWizard.ARPergefactor.Config;
using MadWizard.ARPergefactor.Neighborhood;
using MadWizard.ARPergefactor.Wake.Methods;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PacketDotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.ARPergefactor.Wake.Trigger
{
    internal class WakeOnWOL : IWakeTrigger
    {
        public required ILogger<WakeOnWOL> Logger { private get; init; }

        public required Network Network { private get; init; }

        NetworkWatchHost? IWakeTrigger.Examine(EthernetPacket packet, out bool skipFilters)
        {
            skipFilters = false;

            if (packet.IsWakeOnLAN(Network, out var wol))
            {
                if (Network.Hosts[wol!.DestinationAddress] is NetworkWatchHost host)
                {
                    if (host is VirtualWatchHost virt)
                    {
                        skipFilters = virt.Rediretion.HasFlag(WakeOnLANRedirection.SkipFiltersOnMagicPacket);

                        return host;
                    }
                    else
                    {
                        host.LastWake = DateTime.Now;

                        _ = Logger.LogEvent(null, "Observed", host, packet);
                    }
                }
            }

            return null;
        }
    }
}
