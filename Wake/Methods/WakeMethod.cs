namespace MadWizard.ARPergefactor.Wake.Methods
{
    public readonly struct WakeMethod
    {
        public WakeLayer Layer { get; init; }
        public WakeTransmissionType Route { get; init; }
        public ushort Port { get; init; }

        public TimeSpan Timeout { get; init; }
        public TimeSpan Latency { get; init; }

        public bool Forward { get; init; }
        public bool Silent { get; init; }
    }

    public enum WakeLayer
    {
        Link = 1,
        Network = 2
    }

    public enum WakeTransmissionType
    {
        Broadcast = 0,
        Unicast = 1
    }

    public enum WakeOnLANRedirection
    {
        OnlyIfNotFiltered = 0,

        Always = 1,
    }
}
