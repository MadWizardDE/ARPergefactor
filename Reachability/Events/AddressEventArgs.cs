using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.ARPergefactor.Reachability.Events
{
    public class AddressEventArgs(IPAddress ip) : EventArgs
    {
        public IPAddress IPAddress => ip;
    }
}
