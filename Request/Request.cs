using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using PacketDotNet;

namespace MadWizard.ARPergefactor.Request
{
    internal class Request
    {
        public Packet? TriggerPacket { get; set; }

        public PhysicalAddress? SourcePhysicalAddress
        {
            get
            {
                if (TriggerPacket?.Extract<EthernetPacket>() is EthernetPacket ethernet)
                    return ethernet.SourceHardwareAddress;

                return null;
            }
        }

        public IPAddress? SourceIPAddress
        {
            get
            {
                if (TriggerPacket?.Extract<ArpPacket>() is ArpPacket arp)
                    return arp.SenderProtocolAddress;
                if (TriggerPacket?.Extract<IPPacket>() is IPPacket ip)
                    return ip.SourceAddress;

                return null;
            }
        }
    }
}
