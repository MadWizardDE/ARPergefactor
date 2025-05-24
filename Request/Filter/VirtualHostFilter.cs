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

        public bool NeedsIPUnicast => false;

        bool IWakeRequestFilter.ShouldFilterPacket(EthernetPacket packet, out bool foundMatch)
        {
            foundMatch = false;

            if (Request.SourcePhysicalAddress != null && Network.FindWakeHostByAddress(Request.SourcePhysicalAddress) is VirtualHost sourceByMac)
                if (Host == sourceByMac.PhysicalHost)
                    return true;

            if (Request.SourceIPAddress != null &&Network.FindWakeHostByAddress(Request.SourceIPAddress) is VirtualHost sourceByIP)
                if (Host == sourceByIP.PhysicalHost)
                    return true;

            return false;
        }
    }
}
