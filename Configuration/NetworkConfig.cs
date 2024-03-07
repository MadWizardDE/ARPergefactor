using System.Net.NetworkInformation;
using System.Net;

namespace MadWizard.ARPergefactor.Config
{
    internal class NetworkConfig
    {
        public required string Interface { get; set; }

        public required IList<RouterInfo> Router { get; set; } = [];
        public required IList<WakeHostInfo> WakeHost { get; set; } = [];
        public required IList<HostInfo> Host { get; set; } = [];

        public IEnumerable<HostInfo> EnumerateHosts()
        {
            foreach (var router in Router) foreach (var host in router.EnumerateHosts())
                yield return host;
            foreach (var wakeHost in WakeHost) foreach (var host in wakeHost.EnumerateHosts())
                yield return host;
            foreach (var plainHost in Host) foreach (var host in plainHost.EnumerateHosts())
                yield return host;
        }
    }

    internal class HostInfo
    {
        public required string Name { get; set; }

        private string? MAC { get; set; }
        private string? IPv4 { get; set; }

        public PhysicalAddress? PhysicalAddress => this.MAC != null ? PhysicalAddress.Parse(this.MAC) : null;
        public IPAddress? IPv4Address => this.IPv4 != null ? IPAddress.Parse(this.IPv4) : null;

        public bool HasAddress(PhysicalAddress? address) => address?.Equals(this.PhysicalAddress) ?? false;
        public bool HasAddress(IPAddress? address) => this.EnumerateIPAddresses().Contains(address);

        public virtual IEnumerable<HostInfo> EnumerateHosts()
        {
            yield return this;
        }
        public virtual IEnumerable<IPAddress> EnumerateIPAddresses()
        {
            if (this.IPv4Address != null)
                yield return IPv4Address;
        }
    }

    internal class RouterInfo : HostInfo
    {

    }

    internal class WakeHostInfo : HostInfo
    {
        public required WakeLayer WakeLayer { get; set; } = WakeLayer.Ethernet;
        public required WakeTarget WakeTarget { get; set; } = WakeTarget.Broadcast;
        public required int WakePort { get; set; } = 9;

        public IList<WakeHostInfo> WakeHost { get; set; } = [];

        public FilterConfig? Filter { get; set; }

        public override IEnumerable<HostInfo> EnumerateHosts()
        {
            yield return this;

            foreach (var host in WakeHost)
                foreach (var childHost in host.EnumerateHosts())
                    yield return childHost;
        }
    }

    internal enum WakeLayer
    {
        Ethernet = 2,
        InterNetwork = 3
    }

    internal enum WakeTarget
    {
        None = 0,
        Unicast = 1,
        Broadcast = 2,
    }

}
