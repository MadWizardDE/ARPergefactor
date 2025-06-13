using PacketDotNet;
using SharpPcap;

namespace MadWizard.ARPergefactor.Neighborhood.Filter
{
    public interface IPacketFilter
    {
        bool FilterIncoming(PacketCapture packet);

        bool FilterOutgoing(Packet packet);
    }
}
