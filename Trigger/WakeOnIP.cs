using MadWizard.ARPergefactor.Config;
using MadWizard.ARPergefactor.Neighborhood;
using MadWizard.ARPergefactor.Neighborhood.Filter;
using MadWizard.ARPergefactor.Request;
using Microsoft.Extensions.Options;
using PacketDotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Intrinsics.Arm;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.ARPergefactor.Trigger
{
    internal class WakeOnIP(KnockerUp knocker) : IWakeTrigger
    {
        public required Network Network { private get; init; }

        bool IWakeTrigger.Handle(EthernetPacket packet)
        {
            if ((packet.Type == EthernetType.IPv4 || packet.Type == EthernetType.IPv6) && packet.PayloadPacket is IPPacket ip)
            {
                if (ip.Protocol == ProtocolType.Tcp || ip.Protocol == ProtocolType.Udp) // only service requests
                {
                    if (Network.FindWakeHostByAddress(ip: ip.DestinationAddress) is NetworkHost host)
                    {
                        knocker.MakeHostAvailable(host, packet);
                    }
                }
            }

            return false;
        }
    }
}