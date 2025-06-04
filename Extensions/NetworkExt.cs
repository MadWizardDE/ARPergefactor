using MadWizard.ARPergefactor.Neighborhood;
using Microsoft.Extensions.Hosting;
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
        public static bool HasBeenSeen(this NetworkWatchHost host, TimeSpan? duration = null)
        {
            return host.LastSeen != null && (DateTime.Now - host.LastSeen) < (duration ?? host.PingMethod.Timeout);
        }

        public static bool HasBeenWokenSince(this NetworkWatchHost host, TimeSpan duration)
        {
            return host.LastWake != null && (DateTime.Now - host.LastWake) < duration;
        }

        public static bool HasSeenVPNClients(this NetworkRouter router, TimeSpan? duration = null)
        {
            return router.LastVPN != null && (DateTime.Now - router.LastVPN) < (duration ?? router.Options.VPNTimeout);
        }

        public static bool HasSentPacket(this NetworkDevice device, EthernetPacket packet)
        {
            return device.PhysicalAddress.Equals(packet.SourceHardwareAddress);
        }

        public static NetworkWatchHost WakeTarget(this NetworkWatchHost host)
        {
            return host is VirtualWatchHost virt ? virt.PhysicalHost : host;
        }

    }
}
