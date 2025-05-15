using MadWizard.ARPergefactor.Config;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.ARPergefactor.Neighborhood
{
    internal readonly struct WakeMethod
    {
        public readonly WakeLayer Layer { get; init; }
        public readonly WakeTransmissionType Target { get; init; }
        public readonly int Port { get; init; }

        public readonly bool Silent { get; init; }

        public TimeSpan? Throttle { get; init; } // TODO wird das gesetzt?
    }

    internal enum WakeLayer
    {
        Link = 1,
        Internet = 2
    }

    internal enum WakeTransmissionType
    {
        Broadcast = 0,
        Unicast = 1
    }
}
