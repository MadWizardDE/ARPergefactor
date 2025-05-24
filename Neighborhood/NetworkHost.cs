using Autofac;
using Autofac.Core;
using Autofac.Core.Lifetime;
using MadWizard.ARPergefactor.Config;
using MadWizard.ARPergefactor.Impersonate;
using MadWizard.ARPergefactor.Neighborhood.Filter;
using MadWizard.ARPergefactor.Neighborhood.Tables;
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
    public class NetworkHost
    {
        public string Name { get; init; }
        public string HostName { get => field ?? Name; set; } = null!;

        public required Network Network { get; init; }
        public required NetworkDevice Device { private get; init; }

        public required ILogger<NetworkHost> Logger { protected get; init; }

        public required ILifetimeScope Scope { private get; init; }

        public PhysicalAddress? PhysicalAddress { get; set; }

        readonly IPTable table = new();
        public IEnumerable<IPAddress> IPAddresses => table;
        public IEnumerable<IPAddress> IPv4Addresses => IPAddresses.Where(ip => ip.AddressFamily == AddressFamily.InterNetwork);
        public IEnumerable<IPAddress> IPv6Addresses => IPAddresses.Where(ip => ip.AddressFamily == AddressFamily.InterNetworkV6);

        public virtual NetworkHost WakeTarget => this;

        public PingMethod? PingMethod { get; set; }
        public PoseMethod? PoseMethod { get; set; }
        public WakeMethod? WakeMethod { get; set; }

        public DateTime? LastSeen { get; protected set { field = value; Seen?.Invoke(this, EventArgs.Empty); } }
        public DateTime? LastUnseen { get; protected set { field = value; Unseen?.Invoke(this, EventArgs.Empty); } }
        public DateTime? LastWake { get; protected set { field = value; Wake?.Invoke(this, EventArgs.Empty); } }

        public event EventHandler<IPEventArgs>? AddressAdded;
        public event EventHandler<IPEventArgs>? AddressRemoved;

        public event EventHandler<IPAdvertisementEventArgs>? AddressAdvertised;

        public event EventHandler<EventArgs>? Seen;
        public event EventHandler<EventArgs>? Unseen;
        public event EventHandler<EventArgs>? Wake;

        public NetworkHost(string name)
        {
            Name = name;

            table.Expired += (sender, args) =>
            {
                this.Logger?.LogDebug("Remove {Family} address '{IPAddress}' from Host '{HostName}' (expired)", args.ToFamilyName(), args, Name);

                AddressRemoved?.Invoke(this, new(args));
            };
        }

        protected void TriggerAddressAdvertisement(IPAddress ip, TimeSpan? lifetime = null)
        {
            Logger.LogDebug("Host '{HostName}' advertised unknown {Family} address '{IPAddress}'", Name, ip.ToFamilyName(), ip);

            AddressAdvertised?.Invoke(this, new(ip, lifetime));
        }

        public ILifetimeScope StartRequest(EthernetPacket trigger, out WakeRequest request)
        {
            var requestScope = Scope.BeginLifetimeScope(MatchingScopeLifetimeTags.RequestLifetimeScopeTag);

            request = requestScope.Resolve<WakeRequest>(TypedParameter.From(trigger));

            return requestScope;
        }

        public bool AddAddress(IPAddress ip, TimeSpan? lifetime = null)
        {
            if (lifetime != null ? table.SetDynamicEntry(ip, lifetime.Value) : table.AddStaticEntry(ip))
            {
                Logger.LogDebug($"Add {ip.ToFamilyName()} address '{ip}' to Host '{Name}'" 
                    + (lifetime != null ? $" with lifetime {lifetime}" : ""));

                AddressAdded?.Invoke(this, new(ip));

                return true;
            }

            return false;
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

        public bool RemoveAddress(IPAddress ip)
        {
            if (table.RemoveEntry(ip))
            {
                Logger.LogDebug("Remove {Family} address '{IPAddress}' from Host '{HostName}'", ip.ToFamilyName(), ip, Name);

                AddressRemoved?.Invoke(this, new(ip));

                return true;
            }

            return false;
        }

        internal virtual void Examine(EthernetPacket packet)
        {
            if (HasAddress(packet.SourceHardwareAddress))
            {
                LastSeen = DateTime.Now;
            }
            else if (packet.PayloadPacket is ArpPacket arp)
            {
                if (HasAddress(ip: arp.SenderProtocolAddress))
                {
                    LastSeen = DateTime.Now;

                    if (arp.IsAnnouncement() && arp.SenderHardwareAddress.Equals(PhysicalAddressExt.Empty))
                    {
                        Logger.LogDebug($"Received ARP Dennouncement from \"{Name}\", triggered by {arp.SenderProtocolAddress}");

                        LastUnseen = DateTime.Now;
                    }
                }
                else if (HasAddress(mac: arp.SenderHardwareAddress) && !arp.SenderProtocolAddress.Equals(IPAddress.Any))
                {
                    if (arp.SenderProtocolAddress is IPAddress spa && !spa.IsAPIPA())
                    {
                        TriggerAddressAdvertisement(arp.SenderProtocolAddress);
                    }

                    LastSeen = DateTime.Now;
                }
            }
            else if (packet.Extract<NdpPacket>() is NdpNeighborAdvertisementPacket ndp)
            {
                if (HasAddress(ip: ndp.TargetAddress))
                {
                    LastSeen = DateTime.Now;
                }
                else if (HasAddress(ndp.FindSourcePhysicalAddress()))
                {
                    if (ndp.TargetAddress is IPAddress ta)
                    {
                        TriggerAddressAdvertisement(ta);
                    }

                    LastSeen = DateTime.Now;
                }
            }
            else if (packet.PayloadPacket is IPPacket ip && HasAddress(ip: ip.SourceAddress))
            {
                LastSeen = DateTime.Now;
            }
        }

        public async Task<TimeSpan> SendICMPEchoRequest(IPAddress? ip = null, TimeSpan? suppliedTimeout = null)
        {
            var timeout = suppliedTimeout ?? PingMethod?.Timeout ?? TimeSpan.Zero;

            using var ping = new Ping();

            var reply = ip != null ?
                await ping.SendPingAsync(ip, timeout, options: new(64, true)) :
                await ping.SendPingAsync(HostName, timeout, options: new(64, true));

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

        public async Task<TimeSpan> DoARPing(IPAddress ip, TimeSpan? suppliedTimeout = null)
        {
            return await DoIPing(Network.SendARPRequest, ip, suppliedTimeout);
        }

        public async Task<TimeSpan> DoNDPing(IPAddress ip, TimeSpan? suppliedTimeout = null)
        {
            return await DoIPing(Network.SendNDPNeighborSolicitation, ip, suppliedTimeout);
        }

        private async Task<TimeSpan> DoIPing(Action<IPAddress> method, IPAddress ip, TimeSpan? suppliedTimeout = null)
        {
            var timeout = suppliedTimeout ?? PingMethod?.Timeout ?? TimeSpan.Zero;

            using SemaphoreSlim semaphorePing = new(0, 1);

            // how can the semaphore be disposed, before handler is removed?
            void handler(object? sender, EventArgs args) { try { if (semaphorePing.CurrentCount == 0) semaphorePing.Release(); } catch (ObjectDisposedException) { } }

            var stopwatch = Stopwatch.StartNew();

            this.Seen += handler;

            try
            {
                method(ip);

                if (await semaphorePing.WaitAsync(timeout))
                {
                    return stopwatch.Elapsed;
                }
            }
            finally
            {
                this.Seen -= handler;
            }

            throw new TimeoutException($"Ping to {Name} timed out after {stopwatch.Elapsed} ms");

        }

        public async Task<bool> WakeUp()
        {
            using SemaphoreSlim semaphorePing = new(0, 1);

            void handler(object? sender, EventArgs args) { if (semaphorePing.CurrentCount == 0) semaphorePing.Release(); }

            this.Seen += handler;

            try
            {
                if (WakeMethod is WakeMethod method && PhysicalAddress != null)
                {
                    var wol = new WakeOnLanPacket(PhysicalAddress);

                    int countSent = 0;
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
                                countSent++;
                                break;
                            }

                        case WakeLayer.Internet:
                            {
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
                                    countSent++;
                                }

                                break;
                            }
                    }

                    if (countSent > 0)
                    {
                        if (!await semaphorePing.WaitAsync(method.Timeout))
                        {
                            throw new WakeTimeoutException(method.Timeout);
                        }
                    }

                    return countSent > 0;
                }

                return false;
            }
            finally
            {
                this.Seen -= handler;
            }
        }
    }

    public class IPEventArgs(IPAddress ip) : EventArgs
    {
        public IPAddress IP => ip;
    }

    public class IPAdvertisementEventArgs(IPAddress ip, TimeSpan? lifetime = null) : IPEventArgs(ip)
    {
        public TimeSpan? Lifetime => lifetime;
    }
}