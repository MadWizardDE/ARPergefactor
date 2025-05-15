using MadWizard.ARPergefactor.Config;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.ARPergefactor.Neighborhood
{
    internal class NetworkRouter(string name) : NetworkHost(name)
    {
        public required NetworkRouterOptions Options { get; init; }
    }
}
