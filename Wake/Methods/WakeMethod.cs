namespace MadWizard.ARPergefactor.Wake.Methods
{
    public readonly struct WakeMethod
    {
        public WakeType Type { get; init; }
        public ushort Port { get; init; }

        public TimeSpan Timeout { get; init; }
        public TimeSpan Latency { get; init; }

        public bool Forward { get; init; }
        public bool Silent { get; init; }
    }

    [Flags]
    public enum WakeType
    {
        Auto = 0,

        Link = 1,
        Network = 2,

        Unicast = 4,
        Broadcast = 8,

        None = 0xFFFF
    }

    public enum WakeOnLANRedirection
    {
        OnlyIfNotFiltered = 0,

        Always = 1,
    }
}
