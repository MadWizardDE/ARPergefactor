using PacketDotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.ARPergefactor.Packets
{
    internal static class ArpPacketExt
    {
        public static bool IsGratuitous(this ArpPacket arp)
        {
            return arp.SenderProtocolAddress.Equals(arp.TargetProtocolAddress);
        }

        public static bool IsProbe(this ArpPacket arp)
        {
            return arp.SenderProtocolAddress.Equals(IPAddress.Any);
        }

    }
}
