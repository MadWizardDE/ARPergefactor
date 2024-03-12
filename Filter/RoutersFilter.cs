using MadWizard.ARPergefactor.Config;
using MadWizard.ARPergefactor.Request;
using Microsoft.Extensions.Options;

namespace MadWizard.ARPergefactor.Filter
{
    internal class RoutersFilter(IOptionsMonitor<WakeConfig> config) : IWakeRequestFilter
    {
        async Task<bool> IWakeRequestFilter.FilterWakeRequest(WakeRequest request)
        {
            if (MatchesFilters(config.CurrentValue.Filter?.BlacklistRouters, request))
                return true;

            foreach (var host in request.Hosts)
                if (MatchesFilters(host.Filter?.BlacklistRouters, request))
                    return true;

            return false;
        }

        private bool MatchesFilters(FilterRoutersEntry? filter, WakeRequest request)
        {
            if (filter != null)
                foreach (var router in request.Network.Router)
                {
                    if (filter.ByMAC && router.HasAddress(request.SourcePhysicalAddress))
                        return true;
                    if (filter.ByIP && router.HasAddress(request.SourceIPAddress))
                        return true;
                }

            return false;
        }
    }
}
