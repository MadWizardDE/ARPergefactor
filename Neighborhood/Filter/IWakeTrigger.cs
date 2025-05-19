using PacketDotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.ARPergefactor.Neighborhood.Filter
{
    public interface IWakeTrigger
    {
        bool Handle(EthernetPacket packet);
    }
}
