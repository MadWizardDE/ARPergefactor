using MadWizard.ARPergefactor.Neighborhood;
using PacketDotNet;

namespace MadWizard.ARPergefactor.Wake
{
    public interface IWakeTrigger
    {
        NetworkWatchHost? Examine(EthernetPacket packet, out bool skipFilters);
    }
}
