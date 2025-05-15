using MadWizard.ARPergefactor.Config;
using MadWizard.ARPergefactor.Neighborhood;
using MadWizard.ARPergefactor.Neighborhood.Filter;
using MadWizard.ARPergefactor.Request;
using Microsoft.Extensions.Options;
using PacketDotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.ARPergefactor.Trigger
{
    internal class WakeOnNDP(KnockerUp knocker) : IEthernetListener
    {
        public required Network Network { private get; init; }

        bool IEthernetListener.Handle(EthernetPacket packet)
        {
            return false; // TODO implement Network Discovery Protocol (NDP)
        }
    }
}
