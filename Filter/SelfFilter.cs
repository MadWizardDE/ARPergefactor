using MadWizard.ARPergefactor.Config;
using MadWizard.ARPergefactor.Filter;
using MadWizard.ARPergefactor.Request;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.ARPergefactor.Filter
{
    internal class SelfFilter(IOptionsMonitor<WakeConfig> config) : IWakeRequestFilter
    {
        bool IWakeRequestFilter.FilterWakeRequest(WakeRequest request)
        {
            var filterSelf = config.CurrentValue.Filter?.BlacklistSelf != null; // global filter?

            foreach (var host in request.Hosts)
                foreach (var childHost in host.EnumerateHosts())
                    if (filterSelf = (filterSelf || host.Filter?.BlacklistSelf != null))
                        if (childHost.HasAddress(request.SourceIPAddress) || childHost.HasAddress(request.SourcePhysicalAddress))
                            return true;

            return false;
        }
    }
}
