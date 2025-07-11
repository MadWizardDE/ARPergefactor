﻿namespace MadWizard.ARPergefactor.Neighborhood.Methods
{
    public readonly struct AutoDetectMethod
    {
        public TimeSpan Timeout { get; init; }
        public TimeSpan? Latency { get; init; }
    }

    [Flags]
    public enum AutoDetectType
    {
        None = 0,

        IPv4 = 1,
        IPv6 = 2,

        Router = 4
    }
}
