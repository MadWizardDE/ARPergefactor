using MadWizard.ARPergefactor.Neighborhood;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.ARPergefactor.Reachability.Events
{
    public class AddressAdvertisement(IPAddress ip, TimeSpan? lifetime = null) : AddressEventArgs(ip)
    {
        public TimeSpan? Lifetime => lifetime;
    }
}
