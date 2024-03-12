using Autofac;
using MadWizard.ARPergefactor.Config;
using MadWizard.ARPergefactor.Request;
using Microsoft.Extensions.Options;
using PacketDotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using MadWizard.ARPergefactor.Packets;
using Microsoft.Extensions.Hosting;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using ARPergefactor.Packet;

namespace MadWizard.ARPergefactor
{
    internal class HeartbeatMonitor(NetworkSniffer sniffer)
    {
        public required ILogger<HeartbeatMonitor> Logger { private get; init; }

        public async Task<bool> CheckIfHostAlive(HostInfo host, uint timeout, uint ping = 1)
        {
            SemaphoreSlim semaphorePing = new(0, 1);

            for (var retry = ping; retry > 0; retry--)
            {
                DateTime timePing = DateTime.Now;

                void handler(object? sender, Packet packet)
                {
                    if (packet.Extract<ArpPacket>() is ArpPacket arp)
                    {
                        if (host.HasAddress(arp.SenderHardwareAddress) && host.HasAddress(arp.SenderProtocolAddress))
                        {
                            TimeSpan latency = DateTime.Now - timePing;

                            Logger.LogDebug($"Received ARPing for \"{host.Name}\" after {Math.Ceiling(latency.TotalMilliseconds)} ms");

                            semaphorePing.Release();
                        }
                    }
                }

                sniffer.PacketReceived += handler;

                try
                {
                    SendARPRequest(host);

                    if (await semaphorePing.WaitAsync((int)timeout))
                        return true;
                }
                finally
                {
                    sniffer.PacketReceived -= handler;
                }
            }

            return false;
        }

        private void SendARPRequest(HostInfo host)
        {
            var response = new EthernetPacket(sniffer.PhysicalAddress, PhysicalAddressExt.Broadcast, EthernetType.Arp)
            {
                PayloadPacket = new ArpPacket(ArpOperation.Request,
                    PhysicalAddressExt.Empty, host.IPv4Address,
                    sniffer.PhysicalAddress, sniffer.IPv4Address)
            };

            sniffer.SendPacket(response);
        }
    }
}
