using MadWizard.ARPergefactor.Config;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.ARPergefactor.Neighborhood
{
    public readonly struct WakeMethod
    {
        public WakeLayer Layer { get; init; }
        public WakeTransmissionType Target { get; init; }
        public int Port { get; init; }

        public bool Silent { get; init; }

        public TimeSpan Timeout { get; init; }
        public TimeSpan Latency { get; init; }
    }

    public enum WakeLayer
    {
        Link = 1,
        Internet = 2
    }

    public enum WakeTransmissionType
    {
        Broadcast = 0,
        Unicast = 1
    }
}
