using MadWizard.ARPergefactor.Config;
using MadWizard.ARPergefactor.Neighborhood;
using Microsoft.Extensions.Options;
using PacketDotNet;
using System.Diagnostics.Metrics;
using System.Net.Sockets;

namespace MadWizard.ARPergefactor.Request.Filter
{
    internal class RouterFilter : IWakeRequestFilter
    {
        public required Network Network { private get; init; }

        public required WakeRequest Request { private get; init; }

        public bool NeedsIPUnicast => SentByRouter(Request.TriggerPacket) is not null;

        bool IWakeRequestFilter.ShouldFilterPacket(EthernetPacket packet, out bool foundMatch)
        {
            foundMatch = false;

            if (SentByRouter(Request.TriggerPacket) is NetworkRouter router)
            {
                if (router.HasAnyVPNClientConnected().Result) // TODO: need to be called async?
                {
                    if (Request.TriggerPacket != packet)
                    {
                        /*
                         * The first non-router packet should determine the actual trigger,
                         * because we don't want to have the router written to the log file,
                         * if a VPN client has triggered the wake request.
                         */
                        if (SentByRouter(packet) is null)
                        {
                            Request.TriggerPacket = packet;

                            return false;
                        }
                        else
                        {
                            return true; // ignore all subsequent packets from the router
                        }
                    }
                    else
                    {
                        return false; // allow the first packet from the router, to start impersonation
                    }
                }
                else
                {
                    return router.Options.AllowWake; // without connected VPN client, it depends on the configuration
                }
            }

            return false;
        }

        private NetworkRouter? SentByRouter(EthernetPacket packet)
        {
            return Network.OfType<NetworkRouter>().Where(router => router.HasAddress(ip: packet.FindSourceIPAddress())).FirstOrDefault();
        }
    }
}
