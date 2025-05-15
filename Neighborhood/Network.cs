using Autofac;
using Microsoft.Extensions.Logging;
using PacketDotNet;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace MadWizard.ARPergefactor.Neighborhood
{
    internal partial class Network(NetworkOptions options) : IIEnumerable<NetworkHost>
    {
        public NetworkOptions Options => options;

        public required NetworkDevice Device { private get; init; }

        public required ILogger<NetworkHost> Logger { private get; init; }

        readonly Dictionary<string, NetworkHost> _hosts = [];

        public void AddHost(NetworkHost host)
        {
            if (_hosts.ContainsKey(host.Name))
                throw new ArgumentException($"Host '{host.Name}' already exists on network '{Device.Name}'.");

            _hosts[host.Name] = host;
        }

        public void StartMonitoring()
        {
            Device.StartCapture();
        }

        public void StopMonitoring()
        {
            Device.StopCapture();
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

            var response = new EthernetPacket(Device.PhysicalAddress, PhysicalAddressExt.Broadcast, EthernetType.Arp)
            {
                PayloadPacket = new ArpPacket(ArpOperation.Request,
                PhysicalAddressExt.Empty, ip, // target
                Device.PhysicalAddress, Device.IPv4Address) // source
            };

            Device.SendPacket(response);
        }

        public IEnumerator<NetworkHost> GetEnumerator()
        {
            return _hosts.Values.GetEnumerator();
        }
    }
}
