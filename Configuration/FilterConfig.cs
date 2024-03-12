namespace MadWizard.ARPergefactor.Config
{
    internal class FilterConfig
    {
        public uint? PingTimeout { get; set; }

        public FilterSelfEntry? BlacklistSelf { get; set; }
        public FilterRoutersEntry? BlacklistRouters { get; set; }

        public IList<FilterHostEntry>? BlacklistHost { get; set; }
        public IList<FilterHostEntry>? WhitelistHost { get; set; }

        public IList<FilterTCPServiceEntry>? WhitelistTCPService { get; set; }

    }

    internal class FilterSelfEntry
    {

    }

    internal class FilterRoutersEntry
    {
        public bool ByMAC { get; set; } = true;
        public bool ByIP { get; set; } = true;
    }

    internal class FilterHostEntry : HostInfo
    {

    }

    internal class FilterServiceEntry
    {
        public uint Timeout { get; set; } = 1000;
    }

    internal class FilterTCPServiceEntry : FilterServiceEntry
    {
        public uint? Port { get; set; }
    }
}
