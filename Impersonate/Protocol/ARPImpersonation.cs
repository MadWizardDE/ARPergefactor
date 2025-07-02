using MadWizard.ARPergefactor.Neighborhood;
using Microsoft.Extensions.Logging;
using PacketDotNet;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace MadWizard.ARPergefactor.Impersonate.Protocol
{
    internal class ARPImpersonation : Impersonation
    {
        public required ILogger<ARPImpersonation> Logger { private get; init; }

        public required Network Network { private get; init; }
        public required NetworkDevice Device { private get; init; }

        private bool _manipulatedByBroadcast = false;
        private readonly HashSet<PhysicalAddress> _manipulatedTargets = [];

        private bool _impersonating = false;

        public ARPImpersonation(IPAddress ip, PhysicalAddress mac)
        {
            if (ip.AddressFamily != AddressFamily.InterNetwork)
                throw new ImpersonationImpossibleException($"Only IPv4 is supported; got '{ip}'");

            _impersonating = true;
        }

        internal override void SendAdvertisement()
        {
            SendARPAnnouncement(IPAddress, PhysicalAddress);
        }

        internal override void ProcessPacket(EthernetPacket packet)
        {
            if (packet.PayloadPacket is ArpPacket arp
                && arp.Operation == ArpOperation.Request && !arp.IsProbe()
                && arp.TargetProtocolAddress.Equals(IPAddress))
            {
                Logger.LogDebug($"Received ARP request for IP {IPAddress}");

                SendARPResponse(arp.TargetProtocolAddress, PhysicalAddress, arp.SenderProtocolAddress, arp.SenderHardwareAddress);
            }
        }

        private void SendARPAnnouncement(IPAddress ip, PhysicalAddress mac, PhysicalAddress? macTarget = null)
        {
            if (macTarget == null)
            {
                Logger.LogDebug($"Send ARP announcement <{ip} -> {mac.ToHexString()}>");

                macTarget = PhysicalAddressExt.Broadcast;

                _manipulatedByBroadcast = true;
            }
            else
            {
                Logger.LogDebug($"Send ARP announcement <{ip} -> {mac.ToHexString()}> to {macTarget.ToHexString()}");
            }

            //var response = new EthernetPacket(PhysicalAddress.Parse("F0-E1-D2-C3-B4-A5"), macTarget, EthernetType.Arp)
            var response = new EthernetPacket(Device.PhysicalAddress, macTarget, EthernetType.Arp)
            {
                PayloadPacket = new ArpPacket(ArpOperation.Request, PhysicalAddressExt.Empty, ip, mac, ip)
            };

            Device.SendPacket(response);
        }

        private void SendARPResponse(IPAddress ip, PhysicalAddress mac, IPAddress ipTarget, PhysicalAddress macTarget)
        {
            _manipulatedTargets.Add(macTarget);

            Logger.LogDebug($"Send ARP response <{ip} -> {mac.ToHexString()}> to {ipTarget}");

            //var response = new EthernetPacket(PhysicalAddress.Parse("F0-E1-D2-C3-B4-A5"), macTarget, EthernetType.Arp)
            var response = new EthernetPacket(Device.PhysicalAddress, macTarget, EthernetType.Arp)
            {
                PayloadPacket = new ArpPacket(ArpOperation.Response, macTarget, ipTarget, mac, ip)
            };

            Device.SendPacket(response);
        }

        internal override void Stop(bool silently = false)
        {
            if (_impersonating)
            {
                _impersonating = false;

                Logger.LogDebug($"Stopping impersonation of IP {IPAddress}{(silently ? " (silently)" : "")}");

                if (!silently && Network.Hosts[IPAddress] is NetworkHost host)
                {
                    if (_manipulatedByBroadcast)
                    {
                        SendARPAnnouncement(IPAddress, host.PhysicalAddress!);
                    }
                    else foreach (var target in _manipulatedTargets)
                        {
                            SendARPAnnouncement(IPAddress, host.PhysicalAddress!, target);
                        }
                }

                base.Stop();
            }
        }
    }
}
