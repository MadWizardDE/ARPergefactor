using Autofac.Core;
using MadWizard.ARPergefactor.Neighborhood;
using MadWizard.ARPergefactor.Neighborhood.Cache;
using Microsoft.Extensions.Logging;
using PacketDotNet;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;

namespace MadWizard.ARPergefactor.Impersonate.Protocol
{
    internal class NDPImpersonation : Impersonation
    {
        public required ILogger<NDPImpersonation> Logger { private get; init; }

        public required ILocalIPCache LocalCache { private get; init; }

        public required Network Network { private get; init; }
        public required NetworkDevice Device { private get; init; }

        private bool _manipulatedByBroadcast = false;
        private readonly HashSet<(IPAddress, PhysicalAddress)> _manipulatedTargets = [];

        private bool _impersonating = false;

        public NDPImpersonation(NetworkDevice device, ILocalIPCache cache, IPAddress ip, PhysicalAddress mac)
        {
            if (ip.AddressFamily != AddressFamily.InterNetworkV6)
                throw new ImpersonationImpossibleException($"Only IPv6 is supported; got '{ip}'");
            if (device.IPv6LinkLocalAddress == null)
                throw new ImpersonationImpossibleException($"Device '{device.Name}' does not have a link-local IPv6 address.");

            cache.Update(ip, mac);

            _impersonating = true;
        }

        internal override void SendAdvertisement()
        {
            SendNDPAdvertisement(IPAddress, PhysicalAddress);
        }

        internal override void ProcessPacket(EthernetPacket packet)
        {
            if (packet.Extract<IPv6Packet>() is IPv6Packet ipv6)
                if (packet.Extract<NdpNeighborSolicitationPacket>() is NdpNeighborSolicitationPacket ndp)
                    if (!ipv6.SourceAddress.Equals(IPAddress.IPv6Any) && ndp.TargetAddress.Equals(IPAddress))
                    {
                        Logger.LogDebug($"Received NDP solicitation for IP {IPAddress}");

                        SendNDPAdvertisement(IPAddress, PhysicalAddress, ipv6.SourceAddress, packet.FindSourcePhysicalAddress());
                    }
        }

        private void SendNDPAdvertisement(IPAddress ip, PhysicalAddress mac, IPAddress? ipTarget = null, PhysicalAddress? macTarget = null, bool unsolicited = false)
        {
            Logger.LogDebug($"Sending NDP advertisement <{ip} -> {mac.ToHexString()}>" 
                + (ipTarget != null ? $" to {ipTarget}" : ""));

            NDPFlags flags = NDPFlags.Override;

            if (ipTarget != null && macTarget != null)
            {
                _manipulatedTargets.Add((ipTarget, macTarget));

                if (unsolicited != true)
                {
                    flags |= NDPFlags.Solicited;
                }
            }
            else if (unsolicited != true)
            {
                _manipulatedByBroadcast = true;
            }

            var ipSource = Device.IPv6LinkLocalAddress;
            ipTarget ??= IPAddressExt.LinkLocalMulticast;
            macTarget ??= ipTarget.DeriveLayer2MulticastAddress();

            var request = new EthernetPacket(Device.PhysicalAddress, macTarget, EthernetType.IPv6)
            {
                PayloadPacket = new IPv6Packet(ipSource, ipTarget).WithNDPNeighborAdvertisement(flags, ip, mac)
            };

            Device.SendPacket(request);
        }

        internal override void Stop(bool silently = false)
        {
            if (_impersonating)
            {
                _impersonating = false;

                Logger.LogDebug($"Stopping impersonation of IP {IPAddress}{(silently ? " (silently)" : "")}");

                LocalCache.Delete(IPAddress);

                if (!silently && Network.Hosts[IPAddress] is NetworkHost host)
                {
                    if (_manipulatedByBroadcast)
                    {
                        SendNDPAdvertisement(IPAddress, host.PhysicalAddress!);
                    }
                    else foreach (var target in _manipulatedTargets)
                    {
                        var (ipTarget, macTarget) = target;

                        SendNDPAdvertisement(IPAddress, host.PhysicalAddress!, ipTarget, macTarget, true);
                    }
                }

                base.Stop();
            }
        }
    }
}
