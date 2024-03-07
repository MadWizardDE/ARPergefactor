using MadWizard.ARPergefactor.Config;
using MadWizard.ARPergefactor.Request;
using Microsoft.Extensions.Options;

namespace MadWizard.ARPergefactor.Filter
{
    internal class WhitelistFilter(IOptionsMonitor<WakeConfig> config) : IWakeRequestFilter
    {
        bool IWakeRequestFilter.FilterWakeRequest(WakeRequest request)
        {
            if (MatchesFilters(config.CurrentValue.Filter?.WhitelistHost, request))
                return true;

            foreach (var host in request.Hosts)
                if (MatchesFilters(host.Filter?.WhitelistHost, request))
                    return true;

            return false;
        }

        private static bool MatchesFilters(IList<FilterHostEntry>? filters, WakeRequest request)
        {
            if (filters != null)
            {
                foreach (var filter in filters)
                    if (filter.HasAddress(request.SourceIPAddress) || filter.HasAddress(request.SourcePhysicalAddress))
                        return false;

                return true;
            }

            return false;
        }
    }
}
