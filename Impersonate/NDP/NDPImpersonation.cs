using Autofac.Core;
using MadWizard.ARPergefactor.Neighborhood;
using Microsoft.Extensions.Logging;
using PacketDotNet;
using System.Net.Sockets;
using System.Net;
using System.Net.NetworkInformation;
using MadWizard.ARPergefactor.Neighborhood.Cache;

namespace MadWizard.ARPergefactor.Impersonate.NDP
{
    internal class NDPImpersonation : Impersonation
    {
        public required ILogger<NDPImpersonation> Logger { private get; init; }

        public required ILocalIPCache LocalCache { private get; init; }

        private bool _impersonating = false;

        public NDPImpersonation(NetworkDevice device, NetworkHost host, IPAddress ip)
        {
            if (ip.AddressFamily != AddressFamily.InterNetworkV6)
                throw new ArgumentException($"Only IPv6 is supported; got '{ip}'");
            if (!host.HasAddress(ip: ip))
                throw new ArgumentException($"Host '{host.Name}' is not configured for IPv6 address: {ip}");
            if (device.IPv6LinkLocalAddress == null)
                throw new ArgumentException($"Device '{device.Name}' does not have a link-local IPv6 address.");
        }

        internal override void StartWith(EthernetPacket? packet = null)
        {
            Logger.LogDebug($"Starting impersonation of '{Host.Name}' with IP {IPAddress}");

            LocalCache.Update(IPAddress, Device.PhysicalAddress);

            if (packet?.Extract<NdpNeighborSolicitationPacket>() is not null && packet.Extract<IPv6Packet>() is IPv6Packet ipv6)
            {
                SendNDPAdvertisement(IPAddress, Device.PhysicalAddress, ipv6.SourceAddress, packet.FindSourcePhysicalAddress());
            }
            else
            {
                SendNDPAdvertisement(IPAddress, Device.PhysicalAddress);
            }

            _impersonating = true;
        }

        private void SendNDPAdvertisement(IPAddress ip, PhysicalAddress mac, IPAddress? ipTarget = null, PhysicalAddress? macTarget = null)
        {
            if (ip.AddressFamily != AddressFamily.InterNetworkV6)
                throw new ArgumentException($"Only IPv6 is supported; got '{ip}'");

            Logger.LogDebug($"Sending NDP advertisement <{ip} -> {mac.ToHexString()}>" 
                + (ipTarget != null ? $" to {ipTarget}" : ""));

            var flags = ipTarget != null ?
                NDPFlags.Override | NDPFlags.Solicited :
                    NDPFlags.Override;

            var ipSource = Device.IPv6LinkLocalAddress;
            ipTarget ??= IPAddressExt.LinkLocalMulticast;
            macTarget ??= ipTarget.DeriveLayer2MulticastAddress();

            var request = new EthernetPacket(Device.PhysicalAddress, macTarget, EthernetType.IPv6)
            {
                PayloadPacket = new IPv6Packet(ipSource, ipTarget).WithNDPNeighborAdvertisement(flags, ip, mac)
            };

            Device.SendPacket(request);
        }

        public override bool Handle(EthernetPacket packet)
        {
            if (packet.Extract<IPv6Packet>() is IPv6Packet ipv6)
                if (packet.Extract<NdpNeighborSolicitationPacket>() is NdpNeighborSolicitationPacket ndp)
                    if (!ipv6.SourceAddress.Equals(IPAddress.IPv6Any) && ndp.TargetAddress.Equals(IPAddress))
                    {
                        Logger.LogDebug($"Received NDP solicitation for '{Host.Name}'");
                        SendNDPAdvertisement(IPAddress, Device.PhysicalAddress, ipv6.SourceAddress, packet.FindSourcePhysicalAddress());
                        return true;
                    }

            return false;
        }

        internal override void Stop(bool silently = false)
        {
            if (_impersonating)
            {
                _impersonating = false;

                if (!silently)
                {
                    SendNDPAdvertisement(IPAddress, Host.PhysicalAddress!);
                }

                LocalCache.Delete(IPAddress);

                Logger.LogDebug($"Stopped impersonation of '{Host.Name}' with IP {IPAddress}{(silently ? " (silently)" : "")}");

                base.Stop();
            }
        }
    }
}
