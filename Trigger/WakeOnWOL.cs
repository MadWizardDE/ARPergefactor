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
        string IWakeTrigger.MethodName => "Reroute";

        WakeRequest? IWakeTrigger.AnalyzeNetworkPacket(NetworkConfig network, EthernetPacket ethernet)
        {
            if (config.CurrentValue.Trigger?.WakeOnWOL != null)
                if (ethernet.Type == EthernetType.WakeOnLan && ethernet.PayloadPacket is WakeOnLanPacket wol)
                {
                    foreach (var host in network.WakeHost)
                    {
                        if (host.HasAddress(wol.DestinationAddress))
                        {
                            return new WakeRequest(network, host, true);
                        }

                        var request = DetermineWakeRequestByPhysicalAddress(network, host, wol.DestinationAddress);

                        if (request != null)
                        {
                            request.AddHost(host);

                            return request;
                        }
                    }
                }

            return null;
        }

        private static WakeRequest? DetermineWakeRequestByPhysicalAddress(NetworkConfig network, WakeHostInfo host, PhysicalAddress target)
        {
            WakeRequest? request = null;

            foreach (var childHost in host.WakeHost)
            {
                if (childHost.HasAddress(target))
                {
                    request = new WakeRequest(network, childHost); break;
                }
                else if (childHost.WakeHost != null)
                {
                    if ((request = DetermineWakeRequestByPhysicalAddress(network, childHost, target)) != null)
                    {
                        request.AddHost(host); break;
                    }
                }
            }

            return request;
        }
    }
}
