using Autofac.Core;
using MadWizard.ARPergefactor.Config;
using MadWizard.ARPergefactor.Logging;
using MadWizard.ARPergefactor.Neighborhood;
using MadWizard.ARPergefactor.Neighborhood.Filter;
using MadWizard.ARPergefactor.Request;
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

namespace MadWizard.ARPergefactor.Trigger
{
    internal class WakeOnWOL(KnockerUp knocker, WakeLogger logger) : IWakeTrigger
    {
        public required Network Network { private get; init; }

        bool IWakeTrigger.Handle(EthernetPacket packet)
        {
            if (packet.IsWakeOnLAN(Network, out var wol))
            {
                if (Network.FindWakeHostByAddress(wol!.DestinationAddress) is NetworkHost host)
                {
                    if (host is VirtualHost virt)
                    {
                        knocker.MakeHostAvailable(host, packet, skipFilters: virt.Rediretion == WakeOnLANRedirection.Always);
                    }
                    else
                    {
                        host.LastWake = DateTime.Now;

                        _ = logger.LogEvent(null, "Observed", host, packet);
                    }
                }
            }

            return false;
        }
    }
}
