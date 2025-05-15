using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.ARPergefactor.Neighborhood.Discovery
{
    internal interface IIPConfigurator
    {
        void ConfigureIPv4(NetworkHost host);
        void ConfigureIPv6(NetworkHost host);
    }
}
