using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.ARPergefactor.Config
{
    internal class TriggerConfig
    {
        public WakeOnARPConfig? WakeOnARP { get; set; }
        public WakeOnWOLConfig? WakeOnWOL { get; set; }
    }

    internal class WakeOnARPConfig
    {
        public uint? WaitTimeout { get; set; }
    }

    internal class WakeOnWOLConfig
    {
        public int? WatchPort { get; set; }
    }
}
