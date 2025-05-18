using System.Net.NetworkInformation;
using System.Net;
using MadWizard.ARPergefactor.Neighborhood;
using MadWizard.ARPergefactor.Neighborhood.Methods;

namespace MadWizard.ARPergefactor.Config
{
    internal class FilterScope
    {
        public IList<HostFilterRuleInfo>? HostFilterRule { get; set; }

        public IList<ServiceFilterRuleInfo>? ServiceFilterRule { get; set; }
        public HTTPFilterRuleInfo? HTTPFilterRule { get; set; }

        public PingFilterRuleInfo? PingFilterRule { get; set; }
    }

    internal class NetworkConfig : FilterScope
    {
        public required string Interface { get; set; } // TODO add name attribute?

        public AutoConfigType Auto { get; set; } = AutoConfigType.None; // TODO how to support "|" syntax?

        public required IList<HostInfo> Host { get; set; } = [];
        public required IList<RouterInfo> Router { get; set; } = [];
        public required IList<PhysicalHostInfo> WatchHost { get; set; } = [];

        internal TimeSpan PingTimeout { get; set; } = TimeSpan.FromMilliseconds(500);

        public uint? WatchUDPPort { get; set; }

        internal TimeSpan WakeTimeout { get; set; } = TimeSpan.FromSeconds(10);
        internal TimeSpan WakeLatency { get; set; } = TimeSpan.FromSeconds(5);
    }

    internal class HostInfo : FilterScope
    {
        public required string Name { get; set; }
        public string? HostName { get; set; }

        private string? MAC { get; set; }
        private string? IPv4 { get; set; }
        private string? IPv6 { get; set; }

        public AutoConfigType? Auto { get; set; }

        public PhysicalAddress? PhysicalAddress => this.MAC != null ? PhysicalAddress.Parse(this.MAC) : null;
        private IPAddress? IPv4Address => this.IPv4 != null ? IPAddress.Parse(this.IPv4) : null;
        private IPAddress? IPv6Address => this.IPv6 != null ? IPAddress.Parse(this.IPv6) : null;

        public IEnumerable<IPAddress> IPAddresses
        {
            get
            {
                if (IPv4Address != null)
                    yield return IPv4Address;
                if (IPv6Address != null)
                    yield return IPv6Address;
            }
        }
    }

    internal class WakeHostInfo : HostInfo
    {
        private WakeLayer WakeLayer { get; set; } = WakeLayer.Link;
        private WakeTransmissionType WakeTarget { get; set; } = WakeTransmissionType.Broadcast;
        private int WakePort { get; set; } = 9;
        private bool Silent { get; set; }

        private TimeSpan? WakeTimeout { get; set; }
        private TimeSpan? WakeLatency { get; set; }

        public WakeMethod MakeWakeMethod(NetworkConfig network) => new()
        {
            Layer = this.WakeLayer,
            Target = this.WakeTarget,
            Port = this.WakePort,

            Silent = this.Silent,

            Timeout = WakeTimeout ?? network.WakeTimeout,
            Latency = WakeLatency ?? network.WakeLatency,
        };

        private TimeSpan PoseTimeout { get; set; } = TimeSpan.FromSeconds(2);
        private TimeSpan? PoseLatency { get; set; }

        public PoseMethod MakePoseMethod(NetworkConfig config) => new()
        {
            Timeout = this.PoseTimeout,
            Latency = this.PoseLatency
        };

        private TimeSpan? PingTimeout { get; set; }

        public PingMethod MakePingMethod(NetworkConfig config) => new()
        {
            Timeout = this.PingTimeout ?? config.PingTimeout
        };
    }

    internal class PhysicalHostInfo : WakeHostInfo
    {
        public IList<VirtualHostInfo> VirtualHost { get; set; } = [];
    }

    internal class VirtualHostInfo : WakeHostInfo
    {

    }

    internal class RouterInfo : HostInfo
    {
        private bool AllowWakeOnLAN { get; set; } = true; // TODO default true/false?

        public NetworkRouterOptions Options => new()
        {
            AllowWakeOnLAN = this.AllowWakeOnLAN
        };
    }
}
