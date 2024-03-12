using MadWizard.ARPergefactor.Config;
using MadWizard.ARPergefactor.Request;
using Microsoft.Extensions.Options;

namespace MadWizard.ARPergefactor.Filter
{
    internal class BlacklistHostFilter(IOptionsMonitor<WakeConfig> config) : IWakeRequestFilter
    {
        async Task<bool> IWakeRequestFilter.FilterWakeRequest(WakeRequest request)
        {
            if (MatchesFilters(config.CurrentValue.Filter?.BlacklistHost, request))
                return true;

            foreach (var host in request.Hosts)
                if (MatchesFilters(host.Filter?.BlacklistHost, request))
                    return true;

            return false;
        }

        private static bool MatchesFilters(IList<FilterHostEntry>? filters, WakeRequest request)
        {
            if (filters != null)
                foreach (var filter in filters)
                    if (filter.HasAddress(request.SourceIPAddress) || filter.HasAddress(request.SourcePhysicalAddress))
                        return true;

            return false;
        }
    }
}