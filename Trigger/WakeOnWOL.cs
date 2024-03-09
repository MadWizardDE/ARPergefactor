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
    internal class WakeOnWOL(IOptionsMonitor<WakeConfig> config, NetworkSniffer sniffer) : IWakeTrigger
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
                // IPv4-Support
                if ((ethernet.Type == EthernetType.IPv4 || ethernet.Type == EthernetType.IPv6)
                    && ethernet.PayloadPacket is IPPacket ip)
                    if (ip.Protocol == ProtocolType.Udp && ip.PayloadPacket is UdpPacket udp)
                        if (udp.DestinationPort == TriggerConfig.WatchPort)
                            if (udp.PayloadPacket is WakeOnLanPacket wol)
                            {
                                if (AnalyzeWOLPacket(network, wol) is WakeRequest request)
                                {
                                    request.SourceIPAddress = ip.SourceAddress;

                                    return request;
                                }
                            }

            }


            return null;
        }

        private WakeRequest? AnalyzeWOLPacket(NetworkConfig network, WakeOnLanPacket wol)
        {
            if (wol.Password.SequenceEqual(sniffer.SessionTag))
                return null; // ignore this session

            foreach (var host in network.WakeHost)
            {
                if (host.HasAddress(wol.DestinationAddress))
                    return new WakeRequest(network, host, true);

                if (DetermineWakeRequestByPhysicalAddress(network, host, wol.DestinationAddress) is WakeRequest request)
                {
                    request.AddHost(host);

                    return request;
                }
            }

            return null;
        }

        private static WakeRequest? DetermineWakeRequestByPhysicalAddress(NetworkConfig network, WakeHostInfo host, PhysicalAddress target)
        {
            foreach (var childHost in host.WakeHost)
            {
                if (childHost.HasAddress(target))
                {
                    return new WakeRequest(network, childHost);
                }
                else if (childHost.WakeHost != null)
                {
                    if (DetermineWakeRequestByPhysicalAddress(network, childHost, target) is WakeRequest request)
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
