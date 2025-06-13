using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace MadWizard.ARPergefactor.Neighborhood.Cache
{
    internal class LocalNDPCache : ILocalIPCache // IMPROVE implement local NDP cache
    {
        public void Delete(IPAddress ip)
        {
            if (ip.AddressFamily != AddressFamily.InterNetworkV6)
                throw new ArgumentException($"Only IPv6 is supported; got '{ip}'");

            throw new NotImplementedException();
        }

        public void Update(IPAddress ip, PhysicalAddress mac)
        {
            if (ip.AddressFamily != AddressFamily.InterNetworkV6)
                throw new ArgumentException($"Only IPv6 is supported; got '{ip}'");

            throw new NotImplementedException();
        }
    }
}
