using System.Net;

namespace MadWizard.ARPergefactor.Reachability.Events
{
    public class AddressAdvertisement(IPAddress ip, TimeSpan? lifetime = null) : AddressEventArgs(ip)
    {
        public TimeSpan? Lifetime => lifetime;
    }
}
