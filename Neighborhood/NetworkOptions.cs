using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.ARPergefactor.Neighborhood
{
    internal struct NetworkOptions
    {
        public NetworkOptions()
        {

        }

        public TimeSpan ThrottleTimeout { get; set; } = TimeSpan.Zero;

        public uint? WatchUDPPort { get; set; }
    }
}
