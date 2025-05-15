using MadWizard.ARPergefactor.Config;
using MadWizard.ARPergefactor.Neighborhood;
using PacketDotNet;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.ARPergefactor.Request.Filter
{
    /**
     * Filter to prevent sending WOL packets from virtual hosts to their physical hosts.
     */
    internal class VirtualHostFilter() : IWakeRequestFilter
    {
        public required Network Network { private get; init; }
        public required NetworkHost Host { private get; init; }

        public required WakeRequest Request { private get; init; }

        async Task<bool?> IWakeRequestFilter.ShouldFilterPacket(EthernetPacket packet)
        {
            if (Network.FindHostByAddress(Request.SourcePhysicalAddress, ip: Request.SourceIPAddress) is VirtualHost source)
                if (Host == source.PhysicalHost)
                    return true;

            return false;
        }
    }
}
