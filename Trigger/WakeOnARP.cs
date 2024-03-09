using MadWizard.ARPergefactor.Config;
using MadWizard.ARPergefactor.Packet;
using MadWizard.ARPergefactor.Request;
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

                    foreach (var host in network.WakeHost)
                        if (DetermineWakeRequestByIPAddress(network, host, arp.TargetProtocolAddress) is WakeRequest request)
                        {
                            request.SourceIPAddress = arp.SenderProtocolAddress;

                            return request;
                        }
                }

            return null;
        }

        private static WakeRequest? DetermineWakeRequestByIPAddress(NetworkConfig network, WakeHostInfo host, IPAddress target)
        {
            if (host.HasAddress(target))
                return new WakeRequest(network, host);

            foreach (var childHost in host.WakeHost)
                if (DetermineWakeRequestByIPAddress(network, childHost, target) is WakeRequest request)
                    return request.AddHost(host);

            return null;
        }

    }
}
