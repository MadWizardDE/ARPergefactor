using PacketDotNet;
using System.Net;
using System.Net.NetworkInformation;

namespace MadWizard.ARPergefactor.Impersonate
{
    internal abstract class Impersonation
    {
        public required IPAddress IPAddress { get; init; }
        public required PhysicalAddress PhysicalAddress { get; init; }

        public event EventHandler? Stopped;

        internal abstract void SendAdvertisement();

        internal abstract void ProcessPacket(EthernetPacket packet);

        internal virtual void Stop(bool silently = false)
        {
            Stopped?.Invoke(this, EventArgs.Empty);
        }
    }

    internal class ImpersonationImpossibleException(string? message = null) : Exception(message)
    {

    }
}
