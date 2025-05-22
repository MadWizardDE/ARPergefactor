using Autofac;
using Autofac.Core;
using MadWizard.ARPergefactor.Impersonate;
using MadWizard.ARPergefactor.Neighborhood.Filter;
using Microsoft.Extensions.Logging;
using PacketDotNet;
using System.Collections.Concurrent;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace MadWizard.ARPergefactor.Neighborhood
{
    public class Network(NetworkOptions options) : IIEnumerable<NetworkHost>
    {
        public NetworkOptions Options => options;

        public required NetworkDevice Device { private get; init; }

        public required ILogger<NetworkHost> Logger { private get; init; }

        public IEnumerable<IWakeTrigger> Triggers { private get; init; } = [];

        public event EventHandler? MonitoringStarted;
        public event EventHandler? MonitoringStopped;

        readonly Dictionary<string, NetworkHost> _hosts = [];
        readonly ConcurrentDictionary<IPAddress, Impersonation> _impersonations = [];

        public void AddHost(NetworkHost host)
        {
            if (_hosts.ContainsKey(host.Name))
                throw new ArgumentException($"Host '{host.Name}' already exists on network '{Device.Name}'.");

            _hosts[host.Name] = host;
        }

        public void StartMonitoring()
        {
            Device.EthernetCaptured += HandlePacket;

            Device.StartCapture();

            MonitoringStarted?.Invoke(this, EventArgs.Empty);
        }

        internal void RegisterImpersonation(Impersonation imp)
        {
            if (!_impersonations.ContainsKey(imp.IPAddress))
            {
                _impersonations[imp.IPAddress] = imp;

                imp.Stopped += (sender, args) =>
                {
                    _impersonations.Remove(imp.IPAddress, out _);
                };
            }
            else
                throw new ArgumentException($"Impersonation for IP '{imp.IPAddress}' already exists on network '{Device.Name}'.");
        }

        public bool IsImpersonating(IPAddress ip)
        {
            if (_impersonations.IsEmpty)
                return false;

            return _impersonations.ContainsKey(ip);
        }

        internal bool IsImpersonating(IPAddress ip, out Impersonation? imp)
        {
            imp = null;

            if (IsImpersonating(ip))
            {
                imp = _impersonations[ip];

                return true;
            }

            return false;
        }

        private void HandlePacket(object? sender, EthernetPacket packet)
        {
            foreach (var host in this) // maybe use address dictionary?
                host.Examine(packet);

            if (Options.WatchScope == WatchScope.Network
                || Options.WatchScope == WatchScope.Host && Device.HasSentPacket(packet))
            {
                // has any impersonation handled the packet?
                if (_impersonations.Values.Aggregate(false, (filter, imp) => filter || imp.Handle(packet)))
                    return;

                // notify triggers
                foreach (var trigger in Triggers)
                    if (trigger.Handle(packet))
                        return;
            }
        }

        public void StopMonitoring()
        {
            MonitoringStopped?.Invoke(this, EventArgs.Empty);

            foreach (var imp in _impersonations.Values.ToArray())
                imp.Stop();

            Device.StopCapture();

            Device.EthernetCaptured -= HandlePacket;
        }

        public NetworkHost this[string name]
        {
            get
            {
                if (_hosts.TryGetValue(name, out var host))
                    return host;

                throw new KeyNotFoundException($"Host '{name}' not found on network '{Device.Name}'.");
            }
        }

        public NetworkHost? FindHostByAddress(PhysicalAddress? mac = null, IPAddress? ip = null, bool both = false)
        {
            foreach (var host in this)
                if (host.HasAddress(mac, ip, both))
                    return host;

            return null;
        }

        public NetworkHost? FindWakeHostByAddress(PhysicalAddress? mac = null, IPAddress? ip = null, bool both = false)
        {
            if (FindHostByAddress(mac, ip, both) is NetworkHost host)
                if (host.WakeMethod != null)
                    return host;
            return null;
        }

        public void SendARPRequest(IPAddress ip)
        {
            if (ip.AddressFamily != AddressFamily.InterNetwork)
                throw new ArgumentException($"Only IPv4 is supported; got '{ip}'");

            Logger.LogDebug($"Sending ARP request for {ip}");

            var request = new EthernetPacket(Device.PhysicalAddress, PhysicalAddressExt.Broadcast, EthernetType.Arp)
            {
                PayloadPacket = new ArpPacket(ArpOperation.Request,
                PhysicalAddressExt.Empty, ip, // target
                Device.PhysicalAddress, Device.IPv4Address) // source
            };

            Device.SendPacket(request);
        }

        public void SendNDPSolicitation(IPAddress ip)
        {
            if (ip.AddressFamily != AddressFamily.InterNetworkV6)
                throw new ArgumentException($"Only IPv6 is supported; got '{ip}'");
            if (Device.IPv6LinkLocalAddress == null)
                throw new ArgumentException($"Device '{Device.Name}' does not have a link-local IPv6 address.");

            Logger.LogDebug($"Sending NDP solicitation for {ip}");

            var ipSource = Device.IPv6LinkLocalAddress;
            var ipTarget = ip.DeriveIPv6SolicitedNodeMulticastAddress();

            var request = new EthernetPacket(Device.PhysicalAddress, ipTarget.DeriveLayer2MulticastAddress(), EthernetType.IPv6)
            {
                PayloadPacket = new IPv6Packet(ipSource, ipTarget).WithNDPNeighborSolicitation(ip, Device.PhysicalAddress)
            };

            Device.SendPacket(request);
        }

        public IEnumerator<NetworkHost> GetEnumerator()
        {
            return _hosts.Values.GetEnumerator();
        }

    }
}
