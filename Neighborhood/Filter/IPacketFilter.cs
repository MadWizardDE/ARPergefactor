using PacketDotNet;
using SharpPcap;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.ARPergefactor.Neighborhood.Filter
{
    internal interface IPacketFilter
    {
        bool FilterIncoming(PacketCapture packet);

        bool FilterOutgoing(Packet packet);
    }
}
