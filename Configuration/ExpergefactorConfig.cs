using MadWizard.ARPergefactor.Neighborhood;
using MadWizard.ARPergefactor.Neighborhood.Methods;
using PacketDotNet;

namespace MadWizard.ARPergefactor.Config
{
    internal class ExpergefactorConfig
    {
        public required int Version { get; set; }

        public WatchScope? Scope { get; set; }

        public IList<NetworkConfig>? Network { get; set; }
    }
}
