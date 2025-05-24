using MadWizard.ARPergefactor.Config;
using PacketDotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Net.NetworkInformation;

namespace MadWizard.ARPergefactor.Neighborhood
{
    internal class NetworkRouter(string name) : NetworkHost(name)
    {
        public required NetworkRouterOptions Options { get; init; }

        public required IEnumerable<NetworkHost> VPNClients { private get; init; }

        public DateTime? LastVPN { get; private set; }

        public NetworkHost? FindVPNClient(IPAddress? ip)
        {
            foreach (var host in VPNClients)
                if (host.HasAddress(ip:ip))
                    return host;

            return null;
        }

        public async Task<bool> HasAnyVPNClientConnected(TimeSpan? suppliedTimeout = null)
        {
            var timeout = suppliedTimeout ?? Options.VPNTimeout;

            if ((DateTime.Now - LastVPN) < timeout)
                return true;

            Logger.LogDebug($"Checking router '{Name}' for VPN clients...");

            List<Task<bool>> pings = [];
            foreach (var host in VPNClients)
                foreach (var ip in host.IPAddresses)
                    pings.Add(PingVPNClient(host, ip, timeout));

            if (pings.Count > 0)
            {
                await Task.WhenAll(pings);

                if (pings.Any(ping => ping.Result))
                {
                    LastVPN = DateTime.Now;

                    return true;
                }
            }

            return false;
        }

        private async Task<bool> PingVPNClient(NetworkHost host, IPAddress ip, TimeSpan timeout)
        {
            try
            {
                await host.SendICMPEchoRequest(ip, timeout);

                Logger.LogDebug($"VPN client '{host.Name}' is reachable at {ip}.");

                return true;
            }
            catch (TimeoutException)
            {
                return false;
            }
        }

        internal override void Examine(EthernetPacket packet)
        {
            // prevent the router from changing it's IP address, based on ARP advertisements for it's VPN clients

            if (packet.Type == EthernetType.IPv6 && packet.PayloadPacket is IPv6Packet ipv6)
                if (ipv6.Protocol == PacketDotNet.ProtocolType.IcmpV6 && ipv6.PayloadPacket is IcmpV6Packet icmpv6)
                    if (icmpv6.Type == IcmpV6Type.RouterAdvertisement && icmpv6.PayloadPacket is NdpRouterAdvertisementPacket ndp)
                    {
                        if (HasAddress(packet.FindSourcePhysicalAddress()))
                        {
                            TriggerAddressAdvertisement(ipv6.SourceAddress, TimeSpan.FromSeconds(ndp.RouterLifetime));

                            LastSeen = DateTime.Now;
                        }
                    }
        }
    }
}
