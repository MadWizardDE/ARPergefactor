using Autofac;
using Autofac.Core;
using Autofac.Core.Lifetime;
using MadWizard.ARPergefactor.Config;
using MadWizard.ARPergefactor.Impersonate;
using MadWizard.ARPergefactor.Neighborhood.Filter;
using MadWizard.ARPergefactor.Request;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PacketDotNet;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.ARPergefactor.Neighborhood
{
    internal class NetworkHost(string name) : IEthernetListener
    {
        public string Name => name;
        public string HostName { get => field ?? name; set; } = null!;

        public required Network Network { get; init; }
        public required NetworkDevice Device { private get; init; }
        public required Imposter Imposter { private get; init; }

        public required ILogger<NetworkHost> Logger { private get; init; }

        public required ILifetimeScope Scope { private get; init; }

        public PhysicalAddress? PhysicalAddress { get; set; }

        public ISet<IPAddress> IPAddresses { get; set; } = new HashSet<IPAddress>();
        public IEnumerable<IPAddress> IPv4Addresses => IPAddresses.Where(ip => ip.AddressFamily == AddressFamily.InterNetwork);
        public IEnumerable<IPAddress> IPv6Addresses => IPAddresses.Where(ip => ip.AddressFamily == AddressFamily.InterNetworkV6);

        public virtual NetworkHost WakeTarget => this;

        public PingMethod? PingMethod { get; set; }
        public PoseMethod? PoseMethod { get; set; }
        public WakeMethod? WakeMethod { get; set; }

        public DateTime? LastSeen { get; private set { field = value; Seen?.Invoke(this, EventArgs.Empty); } }
        public DateTime? LastUnseen { get; private set { field = value; Unseen?.Invoke(this, EventArgs.Empty); } }
        public DateTime? LastWake { get; private set { field = value; Wake?.Invoke(this, EventArgs.Empty); } }

        public event EventHandler<EventArgs>? Seen;
        public event EventHandler<IPAddress>? AddressFound;
        public event EventHandler<EventArgs>? Unseen;
        public event EventHandler<EventArgs>? Wake;

        public ILifetimeScope StartRequest(EthernetPacket trigger, out WakeRequest request)
        {
            var requestScope = Scope.BeginLifetimeScope(MatchingScopeLifetimeTags.RequestLifetimeScopeTag);

            request = requestScope.Resolve<WakeRequest>(TypedParameter.From(trigger));

            return requestScope;
        }

        public bool HasAddress(PhysicalAddress? mac = null, IPAddress? ip = null, bool both = false)
        {
            if (mac != null || ip != null)
            {
                bool hasMac = mac != null && mac.Equals(this.PhysicalAddress);
                bool hasIP = ip != null && this.IPAddresses.Contains(ip);

                return both ? hasMac && hasIP : hasMac || hasIP;
            }

            return false;
        }

        bool IEthernetListener.Handle(EthernetPacket packet)
        {
            if (HasAddress(packet.SourceHardwareAddress))
            {
                LastSeen = DateTime.Now;

                if (packet.PayloadPacket is ArpPacket arp)
                {
                    if (HasAddress(ip: arp.SenderProtocolAddress))
                    {
                        LastSeen = DateTime.Now;

                        if (arp.IsAnnouncement() && arp.SenderHardwareAddress.Equals(PhysicalAddressExt.Empty))
                        {
                            Logger.LogInformation($"Received Unmagic Packet from \"{Name}\", triggered by {arp.SenderProtocolAddress}");

                            LastUnseen = DateTime.Now;
                        }
                    }
                    else if (HasAddress(mac: arp.SenderHardwareAddress))
                    {
                        AddressFound?.Invoke(this, arp.SenderProtocolAddress);

                        LastSeen = DateTime.Now;
                    }
                }

                if (packet.PayloadPacket is IPPacket ip && HasAddress(ip: ip.SourceAddress))
                {
                    LastSeen = DateTime.Now;
                }
            }

            return Imposter.Handle(packet); // delegate to Imposter
        }

        public async Task<TimeSpan> SendICMPEchoRequest(TimeSpan timeout)
        {
            using var ping = new Ping();

            var reply = await ping.SendPingAsync(HostName, timeout, options: new(64, true));

            switch (reply.Status)
            {
                case IPStatus.Success:
                    LastSeen = DateTime.Now;

                    return TimeSpan.FromMilliseconds(reply.RoundtripTime);

                case IPStatus.TimedOut:
                    throw new TimeoutException($"Ping to '{Name}' timed out after {timeout} ms");

                default:
                    throw new PingException($"Ping to '{Name}' failed: {reply.Status}");
            }
        }

        public async Task<TimeSpan> DoARPing(IPAddress ip, TimeSpan timeout)
        {
            using SemaphoreSlim semaphorePing = new(0, 1);

            void handler(object? sender, EventArgs args) { if (semaphorePing.CurrentCount == 0) semaphorePing.Release(); }

            var stopwatch = Stopwatch.StartNew();

            this.Seen += handler;

            try
            {
                Network.SendARPRequest(ip);

                if (await semaphorePing.WaitAsync(timeout))
                {
                    return stopwatch.Elapsed;
                }
            }
            finally
            {
                this.Seen -= handler;
            }

            throw new TimeoutException($"ARPing to {Name} timed out after {stopwatch.Elapsed} ms");
        }

        public async Task<TimeSpan> DoNDPing(IPAddress ip, TimeSpan timeout)
        {
            throw new NotImplementedException();
        }

        public bool WakeUp()
        {
            if (WakeMethod is WakeMethod method && PhysicalAddress != null)
            {
                var wol = new WakeOnLanPacket(PhysicalAddress);

                switch (method.Layer)
                {
                    case WakeLayer.Link when Device.PhysicalAddress is PhysicalAddress source:
                    {
                        var target = method.Target == WakeTransmissionType.Unicast ? PhysicalAddress : PhysicalAddressExt.Broadcast;

                        Device.SendPacket(new EthernetPacket(source, target, EthernetType.WakeOnLan)
                        {
                            PayloadPacket = wol
                        });

                        LastWake = DateTime.Now;

                        return true;
                    }

                    case WakeLayer.Internet:
                    {
                        var sentPackets = 0;
                        foreach (var target in method.Target == WakeTransmissionType.Unicast ? IPAddresses.ToList() : [IPAddress.Broadcast])
                        {
                            UdpClient udp = new();
                            if (method.Target == WakeTransmissionType.Broadcast)
                            {
                                udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, 1);
                                udp.EnableBroadcast = true;
                            }

                            udp.Send(wol.Bytes, new IPEndPoint(target, method.Port));

                            LastWake = DateTime.Now;

                            sentPackets++;
                        }

                        return sentPackets > 0;
                    }
                }
            }

            return false;
        }
    }
}
