using MadWizard.ARPergefactor.Impersonate;
using MadWizard.ARPergefactor.Neighborhood;
using MadWizard.ARPergefactor.Neighborhood.Filter;
using MadWizard.ARPergefactor.Reachability.Events;
using MadWizard.ARPergefactor.Wake.Methods;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PacketDotNet;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.Intrinsics.Arm;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace MadWizard.ARPergefactor.Reachability
{
    public class ReachabilityService : INetworkService
    {
        public required ILogger<ReachabilityService> Logger { private get; init; }

        public required Network Network { private get; init; }
        public required NetworkDevice Device { private get; init; }

        public event EventHandler<HostAddressAdvertisement>? HostAddressAdvertisement;
        public event EventHandler<RouterAdvertisement>? RouterAdvertisement;

        readonly HashSet<ReachabilityTest> _currentTests = [];

        void INetworkService.Startup()
        {
            if (Device.IPv6LinkLocalAddress != null)
            {
                SendNDPRouterSolicitation();
            }
        }

        void INetworkService.ProcessPacket(EthernetPacket packet)
        {
            if (packet.PayloadPacket is ArpPacket arp)
            {
                if (Network.Hosts[arp.SenderProtocolAddress] is NetworkHost hostByIP)
                {
                    MaybeAdvertiseAddress(hostByIP, arp.SenderProtocolAddress);
                }

                /**
                 * Some routers do Proxy ARP for their VPN clients, so we need to ignore these IPs then.
                 */
                else if (Network.Hosts[arp.SenderHardwareAddress] is NetworkHost hostByMAC && hostByMAC is not NetworkRouter)
                {
                    if (!arp.SenderProtocolAddress.IsEmpty() && !arp.SenderProtocolAddress.IsAPIPA())
                    {
                        MaybeAdvertiseAddress(hostByMAC, arp.SenderProtocolAddress);
                    }
                }
            }

            else if (packet.Extract<NdpPacket>() is NdpNeighborAdvertisementPacket ndpNeighbor)
            {
                if (Network.Hosts[ndpNeighbor.TargetAddress] is NetworkHost hostByIP)
                {
                    MaybeAdvertiseAddress(hostByIP, ndpNeighbor.TargetAddress);
                }
                else if (Network.Hosts[ndpNeighbor.FindSourcePhysicalAddress()!] is NetworkHost hostByMAC)
                {
                    if (!ndpNeighbor.TargetAddress.IsEmpty())
                    {
                        MaybeAdvertiseAddress(hostByMAC, ndpNeighbor.TargetAddress);
                    }
                }
            }

            else if (packet.Extract<NdpPacket>() is NdpRouterAdvertisementPacket ndpRouter)
            {
                var lifetime = TimeSpan.FromSeconds(ndpRouter.RouterLifetime);
                if (packet.FindSourcePhysicalAddress() is PhysicalAddress mac
                    && packet.FindSourceIPAddress() is IPAddress ip)
                {
                    if (Network.Hosts[mac] is NetworkRouter router)
                    {
                        if (!router.HasAddress(ip: ip))
                        {
                            MaybeAdvertiseAddress(router, ip, lifetime);
                        }
                    }
                    else
                    {
                        RouterAdvertisement?.Invoke(this, new(mac, ip, lifetime));
                    }
                }
            }

            else if (packet.Extract<IcmpV4Packet>() is IcmpV4Packet icmp4 && icmp4.TypeCode == IcmpV4TypeCode.EchoReply
                  || packet.Extract<IcmpV6Packet>() is IcmpV6Packet icmp6 && icmp6.Type == IcmpV6Type.EchoReply)
            {
                if (packet.FindSourceIPAddress() is IPAddress ip && Network.Hosts[ip] is NetworkHost host)
                {
                    MaybeAdvertiseAddress(host, ip);
                }
            }

            else if (packet.IsWakeOnLAN(Network, out var wol) && wol.IsUnmagicPacket(packet))
            {
                if (Network.Hosts[wol!.DestinationAddress] is NetworkWatchHost host)
                {
                    using (Logger.BeginHostScope(host))
                        Logger.Log(LogLevel.Debug, $"Received Unmagic Packet from '{host.Name}', " +
                            $"triggered by {wol.DestinationAddress.ToHexString()}");

                    host.LastUnseen = DateTime.Now;
                }
            }
            else if (packet.PayloadPacket is IPPacket ip && Network.Hosts[ip.SourceAddress] is NetworkWatchHost hostByIP)
            {
                MaybeAdvertiseAddress(hostByIP, ip.SourceAddress);
            }
        }

        private void MaybeAdvertiseAddress(NetworkHost host, IPAddress ip, TimeSpan? lifetime = null)
        {
            if (host is NetworkWatchHost watch)
            {
                watch.LastSeen = DateTime.Now;
            }

            if (!host.HasAddress(ip:ip))
            {
                Logger.LogDebug("Host '{HostName}' advertised unknown {Family} address '{IPAddress}'", host.Name, ip.ToFamilyName(), ip);

                HostAddressAdvertisement?.Invoke(host, new(host, ip, lifetime));
            }

            lock (_currentTests)
            {
                foreach (var test in _currentTests)
                {
                    test.NotifyReachable(ip);
                }
            }
        }

        public async Task<bool> Test(NetworkWatchHost host, IPAddress? address = null)
        {
            if (!host.HasBeenSeen())
            {
                Logger.LogTrace($"Checking reachability of host '{host.Name}'...");

                try
                {
                    TimeSpan latency = await Send(address != null ? new ReachabilityTest([address], host.PingMethod.Timeout) : new HostReachabilityTest(host));

                    Logger.LogDebug($"Received response from '{host.Name}' after {Math.Ceiling(latency.TotalMilliseconds)} ms");

                    return true;
                }
                catch (HostTimeoutException ex)
                {
                    Logger.LogDebug($"Received NO response from '{host.Name}' after {ex.Timeout.TotalMilliseconds} ms");

                    return false; // host is most probably offline
                }
            }

            Logger.LogTrace($"Received last response from '{host.Name}' only {(DateTime.Now - host.LastSeen)?.TotalMilliseconds} ms ago");

            return true; // host was seen lately
        }

        /// <summary>
        /// Tests the reachability of a given host, passively. No requests will be sent to the network.
        /// </summary>
        /// 
        /// <param name="host">Host that should be checked, via all known IP addresses</param>
        /// <param name="timeout">TimeSpan we should wait for a response</param>
        /// 
        /// <returns>Time when we received the first response.</returns>
        /// <exception cref="HostTimeoutException">No response was received in the given TimeSpan</exception>
        public async Task<TimeSpan> Until(NetworkWatchHost host, TimeSpan? timeout = null)
        {
            return await MeasureLatency(new HostReachabilityTest(host, timeout));
        }

        /// <summary>
        /// Sends out requests, to test the reachability of a given host.
        /// </summary>
        /// 
        /// <param name="test">Describes which addresses should be checked and how log we may wait for a response.</param>
        /// <param name="useICMP">Should we use ICMPv4 or ICMPv6 instead of ARP/NDP?</param>
        /// 
        /// <returns>Time until we received the first response. Otherwise throws HostTimeoutException</returns>
        /// <exception cref="NotImplementedException">Unknown address family given</exception>
        /// <exception cref="HostTimeoutException">No response was received in the given TimeSpan</exception>
        public async Task<TimeSpan> Send(ReachabilityTest test, bool useICMP = false)
        {
            foreach (var ip in test)
            {
                if (useICMP)
                    SendICMPEchoRequest(ip);
                else if (ip.AddressFamily == AddressFamily.InterNetwork)
                    SendARPRequest(ip);
                else if (ip.AddressFamily == AddressFamily.InterNetworkV6)
                    SendNDPNeighborSolicitation(ip);
                else
                    throw new NotImplementedException($"Unsupported address family {ip.AddressFamily}");
            }

            return await MeasureLatency(test);
        }

        private async Task<TimeSpan> MeasureLatency(ReachabilityTest test)
        {
            var watch = Stopwatch.StartNew();

            lock (_currentTests) _currentTests.Add(test);

            try
            {
                if (await test.RespondedTimely())
                {
                    return watch.Elapsed;
                }

                throw new HostTimeoutException(watch.Elapsed);
            }
            finally
            {
                lock (_currentTests) _currentTests.Remove(test);

                test.Dispose();
            }
        }

        #region Address Resolution Requests
        public void SendICMPEchoRequest(IPAddress ip)
        {
            using var ping = new Ping();

            // TODO send ping via Device
            ping.SendPingAsync(ip, TimeSpan.Zero, options: new(64, true));
        }

        public void SendARPRequest(IPAddress ip)
        {
            if (ip.AddressFamily != AddressFamily.InterNetwork)
                throw new ArgumentException($"Only IPv4 is supported; got '{ip}'");
            if (Device.IPv4Address == null)
                throw new ArgumentException($"Device '{Device.Name}' does not have a IPv4 address.");

            Logger.LogDebug($"Sending ARP request for {ip}");

            var request = new EthernetPacket(Device.PhysicalAddress, PhysicalAddressExt.Broadcast, EthernetType.Arp)
            {
                PayloadPacket = new ArpPacket(ArpOperation.Request,
                PhysicalAddressExt.Empty, ip, // target
                Device.PhysicalAddress, Device.IPv4Address) // source
            };

            Device.SendPacket(request);
        }

        public void SendNDPNeighborSolicitation(IPAddress ip)
        {
            if (ip.AddressFamily != AddressFamily.InterNetworkV6)
                throw new ArgumentException($"Only IPv6 is supported; got '{ip}'");
            if (Device.IPv6LinkLocalAddress == null)
                throw new ArgumentException($"Device '{Device.Name}' does not have a link-local IPv6 address.");

            Logger.LogDebug($"Sending NDP neighbor solicitation for {ip}");

            var ipSource = Device.IPv6LinkLocalAddress;
            var ipTarget = ip.DeriveIPv6SolicitedNodeMulticastAddress();

            var request = new EthernetPacket(Device.PhysicalAddress, ipTarget.DeriveLayer2MulticastAddress(), EthernetType.IPv6)
            {
                PayloadPacket = new IPv6Packet(ipSource, ipTarget).WithNDPNeighborSolicitation(ip, Device.PhysicalAddress)
            };

            Device.SendPacket(request);
        }

        public void SendNDPRouterSolicitation()
        {
            if (Device.IPv6LinkLocalAddress == null)
                throw new ArgumentException($"Device '{Device.Name}' does not have a link-local IPv6 address.");

            Logger.LogDebug($"Sending NDP router solicitation");

            var ipSource = Device.IPv6LinkLocalAddress;
            var ipTarget = IPAddressExt.LinkLocalRouterMulticast;

            var request = new EthernetPacket(Device.PhysicalAddress, ipTarget.DeriveLayer2MulticastAddress(), EthernetType.IPv6)
            {
                PayloadPacket = new IPv6Packet(ipSource, ipTarget).WithNDPRouterSolicitation()
            };

            Device.SendPacket(request);
        }
        #endregion
    }
}
