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

                    var request = DetermineWakeRequestByIPAddress(network, network.WakeHost, arp.TargetProtocolAddress);

                    if (request != null)
                    {
                        request.SourceIPAddress = arp.SenderProtocolAddress;

                        return request;
                    }
                }

            return null;
        }

        private static WakeRequest? DetermineWakeRequestByIPAddress(NetworkConfig network, IEnumerable<WakeHostInfo> hosts, IPAddress target)
        {
            WakeRequest? request = null;

            foreach (var host in hosts)
            {
                if (host.HasAddress(target))
                {
                    request = new WakeRequest(network, host); break;
                }
                else if (host.WakeHost != null)
                {
                    if ((request = DetermineWakeRequestByIPAddress(network, host.WakeHost, target)) != null)
                    {
                        request.AddHost(host); break;
                    }
                }
            }

            return request;
        }
    }
}
