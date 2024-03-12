using MadWizard.ARPergefactor;
using MadWizard.ARPergefactor.Config;
using MadWizard.ARPergefactor.Filter;
using MadWizard.ARPergefactor.Request;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ARPergefactor.Filter
{
    namespace MadWizard.ARPergefactor.Filter
    {
        internal class PingFilter(IOptionsMonitor<WakeConfig> config, HeartbeatMonitor monitor) : IWakeRequestFilter
        {
            private const uint DEFAULT_TIMEOUT = 500;

            async Task<bool> IWakeRequestFilter.FilterWakeRequest(WakeRequest request)
            {
                if (ShouldPing(request.RequestedHost, out uint timeout))
                {
                    return await monitor.CheckIfHostAlive(request.RequestedHost, timeout);
                }

                return false;
            }

            private bool ShouldPing(WakeHostInfo host, out uint timeout)
            {
                timeout = DEFAULT_TIMEOUT;

                if (host.Filter?.PingTimeout != null)
                {
                    timeout = host.Filter.PingTimeout.Value;
                }

                if (config.CurrentValue.Filter?.PingTimeout != null)
                {
                    timeout = config.CurrentValue.Filter.PingTimeout.Value;
                }

                // TODO für Filterkette implementieren

                return timeout > 0;
            }
        }
    }
}