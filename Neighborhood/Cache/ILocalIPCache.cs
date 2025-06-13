using System.Net;
using System.Net.NetworkInformation;

namespace MadWizard.ARPergefactor.Neighborhood.Cache
{
    internal interface ILocalIPCache
    {
        public void Update(IPAddress ip, PhysicalAddress mac);
        public void Delete(IPAddress ip);
    }
}
