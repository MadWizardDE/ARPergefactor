using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace MadWizard.ARPergefactor.Neighborhood.Cache
{
    internal class MacOSNeighborCache(LocalARPCache arp, LocalNDPCache ndp) : ILocalIPCache
    {
        void ILocalIPCache.Delete(IPAddress ip)
        {
            switch (ip.AddressFamily)
            {
                case AddressFamily.InterNetwork:
                    arp.Delete(ip);
                    break;
                case AddressFamily.InterNetworkV6:
                    ndp.Delete(ip);
                    break;

                default:
                    throw new NotSupportedException($"Address family {ip.AddressFamily} is not supported.");
            }
        }

        void ILocalIPCache.Update(IPAddress ip, PhysicalAddress mac)
        {
            switch (ip.AddressFamily)
            {
                case AddressFamily.InterNetwork:
                    arp.Update(ip, mac);
                    break;
                case AddressFamily.InterNetworkV6:
                    ndp.Update(ip, mac);
                    break;

                default:
                    throw new NotSupportedException($"Address family {ip.AddressFamily} is not supported.");
            }
        }
    }
}
