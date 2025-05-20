using MadWizard.ARPergefactor.Neighborhood;
using PacketDotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.ARPergefactor.Neighborhood
{
    internal static class NetworkExt
    {
        public static bool HasBeenSeen(this NetworkHost host, TimeSpan? duration = null)
        {
            return host.LastSeen != null && (DateTime.Now - host.LastSeen) < (duration ?? host.PingMethod?.Timeout ?? TimeSpan.Zero);
        }

        public static bool HasSentPacket(this NetworkDevice device, EthernetPacket packet)
        {
            return device.PhysicalAddress.Equals(packet.SourceHardwareAddress);
        }

    }
}
