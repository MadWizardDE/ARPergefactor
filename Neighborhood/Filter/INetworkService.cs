using PacketDotNet;

namespace MadWizard.ARPergefactor.Neighborhood.Filter
{
    public interface INetworkService
    {
        void Startup() { }

        void ProcessPacket(EthernetPacket packet);

        void Shutdown() { }
    }
}
