using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.ARPergefactor.Reachability.Events
{
    public class RouterAdvertisement(PhysicalAddress mac, IPAddress ip, TimeSpan lifetime) : AddressAdvertisement(ip, lifetime)
    {
        public PhysicalAddress PhysicalAddress => mac;
    }
}
