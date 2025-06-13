using MadWizard.ARPergefactor.Neighborhood;
using PacketDotNet;

namespace MadWizard.ARPergefactor.Wake.Filter
{
    /**
     * Filter to prevent sending WOL packets from virtual hosts to their physical hosts.
     */
    internal class VirtualHostFilter() : IWakeFilter
    {
        public required Network Network { private get; init; }
        public required NetworkHost Host { private get; init; }

        public required WakeRequest Request { private get; init; }

        public bool NeedsIPUnicast => false;

        bool IWakeFilter.ShouldFilterPacket(EthernetPacket packet, out bool foundMatch)
        {
            foundMatch = false;

            if (Request.SourcePhysicalAddress != null && Network.Hosts[Request.SourcePhysicalAddress] is VirtualWatchHost sourceByMac)
                if (Host == sourceByMac.PhysicalHost)
                    return true;

            if (Request.SourceIPAddress != null &&Network.Hosts[Request.SourceIPAddress] is VirtualWatchHost sourceByIP)
                if (Host == sourceByIP.PhysicalHost)
                    return true;

            return false;
        }
    }
}
