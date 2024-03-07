namespace MadWizard.ARPergefactor.Config
{
    internal class WakeConfig
    {
        public TriggerConfig? Trigger { get; set; }
        public FilterConfig? Filter { get; set; }

        public required NetworkConfig Network { get; set; }
    }
}
