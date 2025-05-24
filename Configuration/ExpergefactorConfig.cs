using MadWizard.ARPergefactor.Neighborhood;
using MadWizard.ARPergefactor.Neighborhood.Methods;
using PacketDotNet;

namespace MadWizard.ARPergefactor.Config
{
    internal class ExpergefactorConfig
    {
        public WatchScope? Scope { get; set; }

        public IList<NetworkConfig>? Network { get; set; }

        private TimeSpan AutoTimeout { get; set; } = TimeSpan.FromSeconds(5);
        private TimeSpan? AutoLatency { get; set; }

        public AutoDetectMethod AutoMethod => new()
        {
            Timeout = this.AutoTimeout,
            Latency = this.AutoLatency
        };
    }
}
