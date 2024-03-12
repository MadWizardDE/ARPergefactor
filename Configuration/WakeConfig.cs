namespace MadWizard.ARPergefactor.Config
{
    internal class WakeConfig
    {
        public uint ThrottleTimeout { get; set; } = 2000;

        public TriggerConfig? Trigger { get; set; }
        public FilterConfig? Filter { get; set; }

        public required NetworkConfig Network { get; set; }
    }
}
