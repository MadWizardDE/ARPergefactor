using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.ARPergefactor.Neighborhood.Methods
{
    public readonly struct AutoMethod
    {
        public TimeSpan Timeout { get; init; }
        public TimeSpan? Latency { get; init; }
    }

    [Flags]
    public enum AutoConfigType
    {
        None = 0,

        IPv4 = 1,
        IPv6 = 2,
    }
}
