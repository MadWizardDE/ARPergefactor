using Autofac.Core;
using MadWizard.ARPergefactor.Impersonate;
using MadWizard.ARPergefactor.Neighborhood.Filter;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PacketDotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.ARPergefactor.Impersonate.NDP
{
    internal class NDPImpersonation : Impersonation<NdpPacket>
    {
        public required ILogger<NDPImpersonation> Logger { private get; init; }

        public override bool Handle(EthernetPacket packet)
        {
            throw new NotImplementedException();
        }

        internal override void StartWith(NdpPacket? packet = null)
        {
            Logger.LogDebug($"Started to impersonate \"{Host.Name}\"");
        }

    }
}
