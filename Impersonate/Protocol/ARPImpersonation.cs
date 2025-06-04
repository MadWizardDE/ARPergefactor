using Autofac.Core;
using MadWizard.ARPergefactor.Neighborhood;
using MadWizard.ARPergefactor.Neighborhood.Cache;
using MadWizard.ARPergefactor.Neighborhood.Filter;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NLog.Targets;
using PacketDotNet;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MadWizard.ARPergefactor.Impersonate.Protocol
{
    internal class ARPImpersonation : Impersonation
    {
        public required ILogger<ARPImpersonation> Logger { private get; init; }

        public required ILocalIPCache LocalCache { private get; init; }

        public required Network Network { private get; init; }
        public required NetworkDevice Device { private get; init; }

        private bool _manipulatedByBroadcast = false;
        private readonly HashSet<PhysicalAddress> _manipulatedTargets = [];

        private bool _impersonating = false;

        public ARPImpersonation(ILocalIPCache cache, IPAddress ip, PhysicalAddress mac)
        {
            if (ip.AddressFamily != AddressFamily.InterNetwork)
                throw new ImpersonationImpossibleException($"Only IPv4 is supported; got '{ip}'");

            cache.Update(ip, mac);

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
                Logger.LogDebug($"Send ARP announcement <{ip} -> {mac.ToHexString()}>  to {macTarget}");

                macTarget = PhysicalAddressExt.Broadcast;

                _manipulatedByBroadcast = true;
            }
            else
            {
                Logger.LogDebug($"Send ARP announcement <{ip} -> {mac.ToHexString()}>");
            }

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

                LocalCache.Delete(IPAddress);

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
