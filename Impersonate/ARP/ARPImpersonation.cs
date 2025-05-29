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
using MadWizard.ARPergefactor.Neighborhood.Cache;

namespace MadWizard.ARPergefactor.Impersonate.ARP
{
    internal class ARPImpersonation : Impersonation
    {
        public required ILogger<ARPImpersonation> Logger { private get; init; }

        public required ILocalIPCache LocalCache { private get; init; }

        private bool _impersonating = false;

        public ARPImpersonation(NetworkHost host, IPAddress ip)
        {
            if (ip.AddressFamily != AddressFamily.InterNetwork)
                throw new ArgumentException($"Only IPv4 is supported; got '{ip}'");
            if (!host.HasAddress(ip: ip))
                throw new ArgumentException($"Host '{host.Name}' is not configured for IPv4 address: {ip}");
        }

        internal override void StartWith(EthernetPacket? packet = null)
        {
            Logger.LogDebug($"Starting impersonation of '{Host.Name}' with IP {IPAddress}");

            LocalCache.Update(IPAddress, Device.PhysicalAddress);

            if (packet?.Extract<ArpPacket>() is ArpPacket arp && arp.Operation == ArpOperation.Request)
            {
                SendARPResponse(arp);
            }
            else
            {
                SendARPAnnouncement(IPAddress, Device.PhysicalAddress);
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

        private void SendARPAnnouncement(IPAddress ip, PhysicalAddress mac)
        {
            var response = new EthernetPacket(Device.PhysicalAddress, PhysicalAddressExt.Broadcast, EthernetType.Arp)
            {
                PayloadPacket = new ArpPacket(ArpOperation.Request, PhysicalAddressExt.Empty, ip, mac, ip)
            };

            Logger.LogDebug($"Send ARP announcement <{ip} -> {mac.ToHexString()}>");

            Device.SendPacket(response);
        }

        private void SendARPResponse(IPAddress ip, PhysicalAddress mac, IPAddress ipTarget, PhysicalAddress macTarget)
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
            SendARPResponse(arp.TargetProtocolAddress, Device.PhysicalAddress!, arp.SenderProtocolAddress, arp.SenderHardwareAddress);
        }

        internal override void Stop(bool silently = false)
        {
            if (_impersonating)
            {
                _impersonating = false;

                Logger.LogDebug($"Stopping impersonation of '{Host.Name}' with IP {IPAddress}{(silently ? " (silently)" : "")}");

                LocalCache.Delete(IPAddress);

                if (!silently)
                {
                    SendARPAnnouncement(IPAddress, Host.PhysicalAddress!);
                }

                base.Stop();
            }
        }
    }
}
