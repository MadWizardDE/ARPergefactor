using MadWizard.ARPergefactor.Config;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.ARPergefactor.Neighborhood
{
    public struct NetworkOptions
    {
        public NetworkOptions()
        {

        }

        public WatchScope? WatchScope { get; set; }

        public uint? WatchUDPPort { get; set; }
    }

    public enum WatchScope
    {
        Network,
        Host,
    }
}
