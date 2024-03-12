using MadWizard.ARPergefactor.Config;
using MadWizard.ARPergefactor.Request;
using Microsoft.Extensions.Options;
using PacketDotNet;

namespace MadWizard.ARPergefactor.Filter
{
    internal class ServiceFilter(Imposter imposter) : IWakeRequestFilter
    {
        async Task<bool> IWakeRequestFilter.FilterWakeRequest(WakeRequest request)
        {
            var services = request.RequestedHost.Filter?.WhitelistTCPService;

            if (services != null)
            {
                var watch = new WatchRequest(request.RequestedHost).AsResponseTo(request);

                uint timeout = 0;
                foreach (var service in services)
                {
                    watch.UntilService(ProtocolType.Tcp, service.Port);

                    timeout = Math.Max(timeout, service.Timeout);
                }

                return !await imposter.ImpersonateUntil(watch, timeout);
            }

            return false;
        }
    }
}
