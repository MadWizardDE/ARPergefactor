using MadWizard.ARPergefactor.Config;
using MadWizard.ARPergefactor.Packet;
using MadWizard.ARPergefactor.Request;
using Microsoft.Extensions.Options;
using PacketDotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.ARPergefactor.Trigger
{
    internal class WakeOnARP(IOptionsMonitor<WakeConfig> config) : IWakeTrigger
    {
        string IWakeTrigger.MethodName => "Send";

        WakeRequest? IWakeTrigger.AnalyzeNetworkPacket(NetworkConfig network, EthernetPacket ethernet)
        {
            if (config.CurrentValue.Trigger?.WakeOnARP != null)
                if (ethernet.Type == EthernetType.Arp && ethernet.PayloadPacket is ArpPacket arp)
                {
                    if (arp.Operation != ArpOperation.Request)
                        return null;
                    if (arp.IsGratuitous() || arp.IsProbe())
                        return null;

                    if (DetermineWakeRequestByIPAddress(network, network.WakeHost, arp.TargetProtocolAddress) is WakeRequest request)
                    {
                        request.SourceIPAddress = arp.SenderProtocolAddress;

                        return request;
                    }
                }

            return null;
        }

        private static WakeRequest? DetermineWakeRequestByIPAddress(NetworkConfig network, IEnumerable<WakeHostInfo> hosts, IPAddress target)
        {
            foreach (var host in hosts)
            {
                if (host.HasAddress(target))
                {
                    return new WakeRequest(network, host);
                }
                else if (host.WakeHost != null)
                {
                    if ((DetermineWakeRequestByIPAddress(network, host.WakeHost, target)) is WakeRequest request)
                    {
                        request.AddHost(host);

                        return request;
                    }
                }
            }

            return null;
        }
    }
}
