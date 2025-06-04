using MadWizard.ARPergefactor.Neighborhood;
using MadWizard.ARPergefactor.Neighborhood.Filter;
using PacketDotNet;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;

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

    internal class ImpersonationImpossibleException(string? message) : Exception(message)
    {

    }
}
