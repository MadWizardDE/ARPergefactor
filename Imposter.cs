using ARPergefactor.Packet;
using Autofac;
using Autofac.Core;
using MadWizard.ARPergefactor.Config;
using MadWizard.ARPergefactor.Request;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PacketDotNet;
using SharpPcap;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.ARPergefactor
{
    internal class Imposter(NetworkSniffer sniffer)
    {
        public required ILogger<Imposter> Logger { private get; init; }

        public async Task<bool> ImpersonateUntil(WatchRequest request, uint timeout)
        {
            if (request.Host.PhysicalAddress == null || request.Host.IPv4Address == null)
            {
                Logger.LogError($"{request.Host.Name} must have MAC and IPv4");

                return false;
            }

            SemaphoreSlim semaphoreMatch = new(0, 1);

            void handler(object? sender, Packet? packet)
            {
                if (packet == null)
                {
                    SendARPAnnouncement(sniffer.PhysicalAddress!, request.Host.IPv4Address);
                }
                else if (packet.Extract<ArpPacket>() is ArpPacket arp && arp.Operation == ArpOperation.Request)
                {
                    if (request.Host.HasAddress(arp.TargetProtocolAddress))
                        SendARPResponse(sniffer.PhysicalAddress!, request.Host.IPv4Address,
                            arp.SenderHardwareAddress, arp.SenderProtocolAddress);
                }
                else if (semaphoreMatch.CurrentCount == 0 && request.Match(packet))
                {
                    Logger.LogTrace($"Received matching packet: {packet}");

                    semaphoreMatch.Release();
                }
            }

            try
            {
                sniffer.PacketReceived += handler;

                handler(this, request.TriggerPacket);

                Logger.LogDebug($"Started to impersonate \"{request.Host.Name}\"");

                return await semaphoreMatch.WaitAsync((int)timeout);
            }
            finally
            {
                sniffer.PacketReceived -= handler;

                SendARPAnnouncement(request.Host.PhysicalAddress, request.Host.IPv4Address);

                Logger.LogDebug($"Stopped to impersonate \"{request.Host.Name}\"");
            }
        }

        private void SendARPAnnouncement(PhysicalAddress mac, IPAddress ip)
        {
            var response = new EthernetPacket(sniffer.PhysicalAddress, PhysicalAddressExt.Broadcast, EthernetType.Arp)
            {
                PayloadPacket = new ArpPacket(ArpOperation.Request, PhysicalAddressExt.Empty, ip, mac, ip)
            };

            sniffer.SendPacket(response);

            Logger.LogDebug($"Sent ARP announcement: {response}");
        }

        private void SendARPResponse(PhysicalAddress mac, IPAddress ip, PhysicalAddress macTarget, IPAddress ipTarget)
        {
            var response = new EthernetPacket(sniffer.PhysicalAddress, macTarget, EthernetType.Arp)
            {
                PayloadPacket = new ArpPacket(ArpOperation.Response, macTarget, ipTarget, mac, ip)
            };

            sniffer.SendPacket(response);

            Logger.LogDebug($"Sent ARP response: {response}");
        }
    }
}
