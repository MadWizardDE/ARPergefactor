using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.ARPergefactor.Impersonate.Methods
{
    public readonly struct PoseMethod
    {
        /// <summary>
        /// Determines if and how long TO impersonate, when a request with insufficient protocl data is received.
        /// </summary>
        public readonly TimeSpan Timeout { get; init; }

        /// <summary>
        /// Determines if and how long NOT to impersonate, after the host becomes unreachable.
        /// </summary>
        public readonly TimeSpan? Latency { get; init; }
    }
}
