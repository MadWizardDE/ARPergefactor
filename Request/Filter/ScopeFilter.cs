using MadWizard.ARPergefactor.Config;
using MadWizard.ARPergefactor.Neighborhood;
using PacketDotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.ARPergefactor.Request.Filter
{
    internal class ScopeFilter(ExpergefactorConfig config) : IWakeRequestFilter
    {
        public required NetworkDevice Device { private get; init; }

        async Task<bool?> IWakeRequestFilter.ShouldFilterPacket(EthernetPacket packet)
        {
            if (config.Scope is WatchScope scope)
            {
                switch (scope)
                {
                    case WatchScope.Network:
                        return false;

                    case WatchScope.Host:
                        throw new NotImplementedException(); // TODO How to check if the packet is from this host?
                }
            }

            return true;
        }
    }
}
