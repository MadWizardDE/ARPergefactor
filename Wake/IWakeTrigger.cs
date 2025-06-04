using MadWizard.ARPergefactor.Neighborhood;
using PacketDotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.ARPergefactor.Wake
{
    public interface IWakeTrigger
    {
        NetworkWatchHost? Examine(EthernetPacket packet, out bool skipFilters);
    }
}
