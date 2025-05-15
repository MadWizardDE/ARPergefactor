using MadWizard.ARPergefactor.Config;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.ARPergefactor.Neighborhood
{
    internal readonly struct PingMethod
    {
        public readonly TimeSpan Timeout { get; init; }
    }
}
