using MadWizard.ARPergefactor.Config;
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
    internal class WakeOnWOL(IOptionsMonitor<WakeConfig> config) : IWakeTrigger
    {
        string IWakeTrigger.MethodName => "Rerouted";

        private WakeOnWOLConfig? TriggerConfig => config.CurrentValue.Trigger?.WakeOnWOL;

        WakeRequest? IWakeTrigger.AnalyzeNetworkPacket(NetworkConfig network, EthernetPacket ethernet)
        {
            if (TriggerConfig != null)
            {
                if (ethernet.Type == EthernetType.WakeOnLan && ethernet.PayloadPacket is WakeOnLanPacket wol)
                    return AnalyzeWOLPacket(network, wol);
            }

            if (TriggerConfig?.WatchPort != null)
            {
                if ((ethernet.Type == EthernetType.IPv4 || ethernet.Type == EthernetType.IPv6)
                    && ethernet.PayloadPacket is IPPacket ip)
                    if (ip.Protocol == ProtocolType.Udp && ip.PayloadPacket is UdpPacket udp)
                        if (udp.DestinationPort == TriggerConfig.WatchPort)
                            if (udp.PayloadPacket is WakeOnLanPacket wol)
                                if (AnalyzeWOLPacket(network, wol) is WakeRequest request)
                                    return request;
            }


            return null;
        }

        private WakeRequest? AnalyzeWOLPacket(NetworkConfig network, WakeOnLanPacket wol)
        {
            foreach (var host in network.WakeHost)
                if (DetermineWakeRequestByPhysicalAddress(network, host, wol.DestinationAddress, true) is WakeRequest request)
                    return request;

            return null;
        }

        private static WakeRequest? DetermineWakeRequestByPhysicalAddress(NetworkConfig network, WakeHostInfo host, PhysicalAddress target, bool observe = false)
        {
            if (host.HasAddress(target))
                return new WakeRequest(network, host, observe);

            foreach (var childHost in host.WakeHost)
                if (DetermineWakeRequestByPhysicalAddress(network, childHost, target) is WakeRequest request)
                    return request.AddHost(host);

            return null;
        }
    }
}
