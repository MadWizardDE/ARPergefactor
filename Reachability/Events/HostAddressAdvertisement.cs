using MadWizard.ARPergefactor.Neighborhood;
using System.Net;

namespace MadWizard.ARPergefactor.Reachability.Events
{
    public class HostAddressAdvertisement(NetworkHost host, IPAddress ip, TimeSpan? lifetime = null) : AddressAdvertisement(ip, lifetime)
    {
        public NetworkHost Host => host;
    }
}
