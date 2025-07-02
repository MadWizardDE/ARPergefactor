using System.Net.NetworkInformation;

namespace MadWizard.ARPergefactor.Reachability.Events
{
    public class PhysicalAddressEventArgs(PhysicalAddress mac) : EventArgs
    {
        public PhysicalAddress PhysicalAddress => mac;
    }
}
