using MadWizard.ARPergefactor.Config;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.ARPergefactor.Neighborhood
{
    internal class VirtualHost(string name) : NetworkHost(name)
    {
        public required NetworkHost PhysicalHost { get; init; }

        public override NetworkHost WakeTarget => PhysicalHost;
    }
}
