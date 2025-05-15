using PacketDotNet;

namespace MadWizard.ARPergefactor.Config
{
    internal class ExpergefactorConfig
    {
        public WatchScope? Scope { get; set; }

        public IList<NetworkConfig>? Network { get; set; }

        public TimeSpan AutoTimeout { get; set; } = TimeSpan.FromSeconds(5);
        public TimeSpan? AutoLatency { get; set; }

        public bool Simulate { get; set; }
    }

    internal enum WatchScope
    {
        Network,
        Host,
    }
}
