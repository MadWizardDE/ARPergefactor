using Autofac.Core;
using MadWizard.ARPergefactor.Config;
using MadWizard.ARPergefactor.Logging;
using MadWizard.ARPergefactor.Neighborhood;
using MadWizard.ARPergefactor.Neighborhood.Filter;
using MadWizard.ARPergefactor.Request;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PacketDotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.ARPergefactor.Trigger
{
    internal class WakeOnWOL(KnockerUp knocker, WakeLogger logger) : IWakeTrigger
    {
        public required Network Network { private get; init; }

        bool IWakeTrigger.Handle(EthernetPacket packet)
        {
            if (packet.Type == EthernetType.WakeOnLan && packet.PayloadPacket is WakeOnLanPacket layer2wol)
                AnalyzeWOLPacket(packet, layer2wol);

            if (Network.Options.WatchUDPPort is uint watchPort)
            {
                if ((packet.Type == EthernetType.IPv4 || packet.Type == EthernetType.IPv6)
                    && packet.PayloadPacket is IPPacket ip)
                    if (ip.Protocol == ProtocolType.Udp && ip.PayloadPacket is UdpPacket udp)
                        if (udp.DestinationPort == watchPort)
                            if (udp.PayloadPacket is WakeOnLanPacket layer3wol)
                                AnalyzeWOLPacket(packet, layer3wol);
            }

            return false;
        }

        private void AnalyzeWOLPacket(EthernetPacket trigger, WakeOnLanPacket wol)
        {
            if (Network.FindWakeHostByAddress(wol.DestinationAddress) is NetworkHost host)
            {
                if (host is VirtualHost)
                {
                    knocker.MakeHostAvailable(host, trigger);
                }
                else
                {
                    _ = logger.LogEvent(null, "Observed", host, trigger);
                }
            }
        }
    }
}
