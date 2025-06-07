using System.Net.NetworkInformation;
using System.Net;
using MadWizard.ARPergefactor.Neighborhood;
using MadWizard.ARPergefactor.Wake.Filter.Rules;
using MadWizard.ARPergefactor.Impersonate.Methods;
using MadWizard.ARPergefactor.Neighborhood.Methods;
using MadWizard.ARPergefactor.Reachability.Methods;
using MadWizard.ARPergefactor.Wake.Methods;
using System.Net.Sockets;

namespace MadWizard.ARPergefactor.Config
{
    internal class NetworkConfig : FilterRuleContainer
    {
        public required string Interface { get; set; }

        public AutoDetectType AutoDetect { get; set; } = AutoDetectType.None;

        private TimeSpan AutoTimeout { get; set; } = TimeSpan.FromSeconds(5);
        private TimeSpan? AutoLatency { get; set; }

        public AutoDetectMethod AutoMethod => new()
        {
            Timeout = this.AutoTimeout,
            Latency = this.AutoLatency
        };

        public required IList<HostInfo> Host { get; set; } = [];
        public required IList<RouterInfo> Router { get; set; } = [];
        public required IList<PhysicalHostInfo> WatchHost { get; set; } = [];

        internal TimeSpan PingTimeout { get; set; } = TimeSpan.FromMilliseconds(500);

        public uint? WatchUDPPort { get; set; }

        internal TimeSpan WakeTimeout { get; set; } = TimeSpan.FromSeconds(10);
        internal TimeSpan WakeLatency { get; set; } = TimeSpan.FromSeconds(5);

        internal bool WakeForward { get; set; } = false;
    }

    internal class HostInfo : FilterRuleContainer
    {
        public required string Name { get; set; }
        public string? HostName { get; set; }

        private string? MAC { get; set; }
        protected string? IPv4 { get; set; }
        protected string? IPv6 { get; set; }

        public AutoDetectType? AutoDetect { get; set; }

        public PhysicalAddress? PhysicalAddress => this.MAC != null ? PhysicalAddress.Parse(this.MAC) : null;
        private IPAddress? IPv4Address => this.IPv4 != null ? IPAddress.Parse(this.IPv4) : null;
        private IPAddress? IPv6Address => this.IPv6 != null ? IPAddress.Parse(this.IPv6) : null;

        public virtual IEnumerable<IPAddress> IPAddresses
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
        public IList<ServiceInfo>? Service { get; set; }

        private WakeLayer WakeLayer { get; set; } = WakeLayer.Link;
        private WakeTransmissionType WakeTarget { get; set; } = WakeTransmissionType.Broadcast;
        private int WakePort { get; set; } = 9;
        private bool Silent { get; set; }

        private TimeSpan? WakeTimeout { get; set; }
        private TimeSpan? WakeLatency { get; set; }

        private bool? WakeForward { get; set; }

        public WakeMethod MakeWakeMethod(NetworkConfig network) => new()
        {
            Layer = WakeLayer,
            Target = WakeTarget,
            Port = WakePort,

            Timeout = WakeTimeout ?? network.WakeTimeout,
            Latency = WakeLatency ?? network.WakeLatency,

            Forward = WakeForward ?? network.WakeForward,

            Silent = Silent,
        };

        private TimeSpan PoseTimeout { get; set; } = TimeSpan.FromSeconds(5);
        private TimeSpan? PoseLatency { get; set; }

        public PoseMethod MakePoseMethod(NetworkConfig config) => new()
        {
            Timeout = PoseTimeout,
            Latency = PoseLatency
        };

        private TimeSpan? PingTimeout { get; set; }

        public PingMethod MakePingMethod(NetworkConfig config) => new()
        {
            Timeout = PingTimeout ?? config.PingTimeout
        };
    }

    internal class PhysicalHostInfo : WakeHostInfo
    {
        public IList<VirtualHostInfo> VirtualHost { get; set; } = [];
    }

    internal class VirtualHostInfo : WakeHostInfo
    {
        public WakeOnLANRedirection WakeRedirect { get; set; } = WakeOnLANRedirection.OnlyIfNotFiltered;
    }

    internal class RouterInfo : HostInfo
    {
        public IList<HostInfo> VPNClient { get; set; } = [];

        private bool AllowWake { get; set; } = false;
        private bool AllowWakeByProxy { get; set; } = false;
        private bool AllowWakeOnLAN { get; set; } = true;

        private TimeSpan VPNTimeout { get; set; } = TimeSpan.FromMilliseconds(500);

        public NetworkRouterOptions Options => new()
        {
            AllowWake = AllowWake,
            AllowWakeByProxy = AllowWakeByProxy,
            AllowWakeOnLAN = AllowWakeOnLAN,

            VPNTimeout = VPNTimeout
        };
    }

    internal class StandardGatewayInfo(NetworkInterface ni, AutoDetectType auto) : RouterInfo
    {
        public override IEnumerable<IPAddress> IPAddresses
        {
            get
            {
                foreach (var gateway in ni.GetIPProperties().GatewayAddresses)
                {
                    if (gateway.Address.AddressFamily == AddressFamily.InterNetwork)
                        if (auto.HasFlag(AutoDetectType.IPv4))
                            yield return gateway.Address;
                    if (gateway.Address.AddressFamily == AddressFamily.InterNetworkV6)
                        if (auto.HasFlag(AutoDetectType.IPv6))
                        {
                            gateway.Address.ScopeId = 0; // ignore scope id
                            yield return gateway.Address;
                        }
                }
            }
        }
    }

    /// <summary>
    /// https://www.iana.org/assignments/service-names-port-numbers/service-names-port-numbers.xml
    /// </summary>
    internal class ServiceInfo : ServiceFilterRuleInfo
    {
        public string? ServiceName { get; set; }

        public ServiceInfo()
        {
            Type = FilterRuleType.Must;
        }
    }
}
