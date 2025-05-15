using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.ARPergefactor.Impersonate.ARP
{
    internal interface ILocalARPCache
    {
        public void Update(PhysicalAddress mac, IPAddress ip);
        public void Delete(IPAddress ip);
    }
}
