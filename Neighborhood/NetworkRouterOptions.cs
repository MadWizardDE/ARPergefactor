using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.ARPergefactor.Neighborhood
{
    internal struct NetworkRouterOptions
    {
        public NetworkRouterOptions()
        {

        }

        public bool AllowWake { get; set; }
        public bool AllowWakeOnLAN { get; set; }

        public TimeSpan VPNTimeout { get; set; }
    }
}
