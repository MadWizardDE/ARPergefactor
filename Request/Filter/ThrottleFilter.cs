using MadWizard.ARPergefactor;
using MadWizard.ARPergefactor.Config;
using MadWizard.ARPergefactor.Neighborhood;
using MadWizard.ARPergefactor.Request;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PacketDotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.ARPergefactor.Request.Filter
{
    /// <summary>
    /// Throttle the amount of wake requests sent to a host.
    /// </summary>
    internal class ThrottleFilter : IWakeRequestFilter
    {
        public required Network Network { private get; init; }
        public required NetworkHost Host { private get; init; }

        async Task<bool?> IWakeRequestFilter.ShouldFilterPacket(EthernetPacket packet)
        {
            if (Host.WakeTarget.LastWake is DateTime last)
            {
                if ((DateTime.Now - last) < Network.Options.ThrottleTimeout)
                    return true;
            }

            return false;
        }
    }
}
