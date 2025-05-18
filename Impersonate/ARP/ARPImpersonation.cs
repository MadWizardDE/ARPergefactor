using Autofac.Core;
using MadWizard.ARPergefactor.Neighborhood;
using Microsoft.Extensions.Hosting;
using PacketDotNet;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MadWizard.ARPergefactor.Neighborhood.Filter;
using System.Security.Cryptography;
using System.Net.Sockets;
using MadWizard.ARPergefactor.Impersonate;

namespace MadWizard.ARPergefactor.Impersonate.ARP
{
    internal class ARPImpersonation : Impersonation<ArpPacket>
    {
        public required ILogger<ARPImpersonation> Logger { private get; init; }

        public required ILocalARPCache LocalCache { private get; init; }

        private bool _impersonating = false;

        public ARPImpersonation(NetworkHost host, IPAddress ip)
        {
            if (ip.AddressFamily != AddressFamily.InterNetwork)
                throw new ArgumentException($"Only IPv4 is supported; got '{ip}'");
            if (!host.HasAddress(ip: ip))
                throw new ArgumentException($"Host '{host.Name}' is not configured for IPv4 address: {ip}");
        }

        internal override void StartWith(ArpPacket? packet = null)
        {
            Logger.LogDebug($"Starting to impersonate '{Host.Name}' with IP {IPAddress}");

            LocalCache.Update(Device.PhysicalAddress, IPAddress);

            if (packet != null && packet.Operation == ArpOperation.Request)
            {
                SendARPResponse(packet);
            }
            else
            {
                SendARPAnnouncement(Device.PhysicalAddress, IPAddress);
            }

            _impersonating = true;
        }

        public override bool Handle(EthernetPacket packet)
        {
            if (packet.PayloadPacket is ArpPacket arp 
                && arp.Operation == ArpOperation.Request && !arp.IsProbe()
                && arp.TargetProtocolAddress.Equals(IPAddress))
            {
                Logger.LogDebug($"Received ARP request for '{Host.Name}'");
                SendARPResponse(arp);
                return true;
            }

            return false;
        }

        private void SendARPAnnouncement(PhysicalAddress mac, IPAddress ip)
        {
            var response = new EthernetPacket(Device.PhysicalAddress, PhysicalAddressExt.Broadcast, EthernetType.Arp)
            {
                PayloadPacket = new ArpPacket(ArpOperation.Request, PhysicalAddressExt.Empty, ip, mac, ip)
            };

            Logger.LogDebug($"Send ARP announcement <{ip} -> {mac.ToHexString()}>");

            Device.SendPacket(response);
        }

        private void SendARPResponse(PhysicalAddress mac, IPAddress ip, PhysicalAddress macTarget, IPAddress ipTarget)
        {
            var response = new EthernetPacket(Device.PhysicalAddress, macTarget, EthernetType.Arp)
            {
                PayloadPacket = new ArpPacket(ArpOperation.Response, macTarget, ipTarget, mac, ip)
            };

            Logger.LogDebug($"Send ARP response <{ip} -> {mac.ToHexString()}> to {ipTarget}");

            Device.SendPacket(response);
        }

        private void SendARPResponse(ArpPacket arp)
        {
            SendARPResponse(Device.PhysicalAddress!, arp.TargetProtocolAddress, arp.SenderHardwareAddress, arp.SenderProtocolAddress);
        }

        internal override void Stop(bool silently = false)
        {
            if (_impersonating)
            {
                _impersonating = false;

                if (!silently)
                {
                    SendARPAnnouncement(Host.PhysicalAddress!, IPAddress);
                }

                LocalCache.Delete(IPAddress);

                Logger.LogDebug($"Stopped to impersonate '{Host.Name}'  with IP {IPAddress}{(silently ? " (silently)" : "")}");

                base.Stop();
            }
        }
    }
}
