using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.ARPergefactor.Neighborhood.Cache
{
    internal interface ILocalIPCache
    {
        public void Update(IPAddress ip, PhysicalAddress mac);
        public void Delete(IPAddress ip);
    }
}
