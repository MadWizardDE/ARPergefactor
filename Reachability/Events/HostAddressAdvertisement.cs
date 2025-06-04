using MadWizard.ARPergefactor.Neighborhood;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.ARPergefactor.Reachability.Events
{
    public class HostAddressAdvertisement(NetworkHost host, IPAddress ip, TimeSpan? lifetime = null) : AddressAdvertisement(ip, lifetime)
    {
        public NetworkHost Host => host;
    }
}
