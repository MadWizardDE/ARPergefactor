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

        public bool AllowWake { get; set; } = false;
        public bool AllowWakeByProxy { get; set; } = false;
        public bool AllowWakeOnLAN { get; set; } = true;

        public TimeSpan VPNTimeout { get; set; }
    }
}
