using System.Net;

namespace MadWizard.ARPergefactor.Reachability.Events
{
    public class AddressEventArgs(IPAddress ip) : EventArgs
    {
        public IPAddress IPAddress => ip;
    }
}
