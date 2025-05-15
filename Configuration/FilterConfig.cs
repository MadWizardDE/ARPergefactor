using MadWizard.ARPergefactor.Request.Filter.Rules;

namespace MadWizard.ARPergefactor.Config
{
    internal class HostFilterRuleInfo : HostInfo
    {
        public bool IsDynamic => !string.IsNullOrWhiteSpace(Name) && PhysicalAddress == null && !IPAddresses.Any();
        public bool IsCompound => ServiceFilterRule?.Count > 0 || HTTPFilterRule != null || PingFilterRule != null;

        public FilterRuleType Type { get; set; } = FilterRuleType.Must;
    }

    internal class ServiceFilterRuleInfo
    {
        public required string Name { get; set; }

        public TransportPortocolType Protocol { get; set; } = TransportPortocolType.TCP;

        public uint Port { get; set; }

        public FilterRuleType Type { get; set; } = FilterRuleType.Must;
    }

    internal class HTTPFilterRuleInfo : ServiceFilterRuleInfo
    {
        public HTTPFilterRuleInfo() 
        {
            Name = "HTTP";
            Protocol = TransportPortocolType.TCP;
            Port = 80;
        }

        public IList<HTTPRequestFilterRuleInfo>? RequestFilterRule { get; set; }
    }

    internal class HTTPRequestFilterRuleInfo
    {
        public string? Path { get; set; }

        public FilterRuleType Type { get; set; } = FilterRuleType.Must;
    }

    internal class PingFilterRuleInfo
    {
        public FilterRuleType Type { get; set; } = FilterRuleType.Must;
    }
}
