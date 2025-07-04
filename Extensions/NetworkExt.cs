﻿using PacketDotNet;
using System.Net;

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

        public static NetworkHost? FindHostByIP(this Network network, IEnumerable<IPAddress> addresses)
        {
            foreach (IPAddress ip in addresses)
            {
                if (network.Hosts[ip] is NetworkHost host)
                    return host;
            }

            return null;
        }
    }
}
