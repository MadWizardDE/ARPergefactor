using MadWizard.ARPergefactor.Request.Filter.Rules;

namespace MadWizard.ARPergefactor.Config
{
    internal class HostFilterRuleInfo : HostInfo
    {
        public bool IsDynamic => !string.IsNullOrWhiteSpace(Name) && PhysicalAddress == null && !IPAddresses.Any();
        public bool IsCompound => ServiceFilterRule?.Count > 0 || HTTPFilterRule != null || PingFilterRule != null;

        public FilterRuleType Type { get; set; } = FilterRuleType.MustNot;
    }

    internal class ServiceFilterRuleInfo
    {
        public required string Name { get; set; }

        public TransportPortocolType Protocol { get; set; } = TransportPortocolType.TCP;

        public uint Port { get; set; }

        public FilterRuleType Type { get; set; } = FilterRuleType.MustNot;
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
        public string? Method { get; set; } // e.g. "GET", "POST", etc.
        public string? Path { get; set; } // /index.html
        public string? Version { get; set; } // e.g. "HTTP/1.1", "HTTP/2.0", etc.
        public string? Host { get; set; } // e.g. "example.com"

        public HTTPUserAgentInfo? UserAgent { get; set; }
        public IList<HTTPHeaderInfo>? Header { get; set; }
        public IList<HTTPCookieInfo>? Cookie { get; set; }

        public FilterRuleType Type { get; set; } = FilterRuleType.Must;

        internal class HTTPHeaderInfo
        {
            public required string Name { get; set; }
            public string? Text { get; set; }
        }

        internal class HTTPUserAgentInfo : HTTPHeaderInfo
        {
            public HTTPUserAgentInfo()
            {
                Name = "User-Agent"; // IMPROVE add attributes to HTTPUserAgentInfo
            }
        }

        internal class HTTPCookieInfo
        {
            public required string Name { get; set; }
            private string? Text { get; set; }
            public string? Value => Text;
        }
    }


    internal class PingFilterRuleInfo
    {
        public FilterRuleType Type { get; set; } = FilterRuleType.MustNot;
    }
}
