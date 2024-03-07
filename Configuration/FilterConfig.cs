namespace MadWizard.ARPergefactor.Config
{
    internal class FilterConfig
    {
        public FilterSelfEntry? BlacklistSelf { get; set; }
        public FilterRoutersEntry? BlacklistRouters { get; set; }

        public IList<FilterHostEntry>? BlacklistHost { get; set; }
        public IList<FilterHostEntry>? WhitelistHost { get; set; }
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
}
