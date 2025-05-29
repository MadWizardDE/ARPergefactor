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

        public bool NeedsIPUnicast => SentByRouter(Request.TriggerPacket) is NetworkRouter router && !router.Options.AllowWake;

        bool IWakeRequestFilter.ShouldFilterPacket(EthernetPacket packet, out bool foundMatch)
        {
            foundMatch = false;

            if (SentByRouter(Request.TriggerPacket) is NetworkRouter router)
            {
                /*
                 * The first non-router packet should determine the actual trigger,
                 * because we don't want to have the router written to the log file,
                 * if a routed client has triggered the wake request.
                 */
                if (Request.TriggerPacket != packet)
                {
                    if (SentByRouter(packet) is null)
                    {
                        Request.TriggerPacket = packet;

                        return false;
                    }

                    /*
                     * Ignore all subsequent packets that are sent by the router,
                     * unless the user has opted out of the router filtering.
                     */
                    return !router.Options.AllowWake;
                }

                /*
                 * Has the user opted out of the router filtering?
                 * 
                 * Otherwise we won't allow the router to trigger a direct wake,
                 * unless another IP (probably from some remote system) has 
                 * actually triggered the wake request.
                 */
                if (router.Options.AllowWake || router.Options.AllowWakeByRemote)
                {
                    return false; // exit early
                }

                /*
                 * Then we check if we deal with a WakeOnLAN packet
                 * and if the user has made an exception to allow WakeOnLAN.
                 */
                else if (router.Options.AllowWakeOnLAN && Request.TriggerPacket.IsWakeOnLAN(Network))
                {
                    return false;
                }

                /*
                 * Then we check if the user has opted in, to allow VPN clients
                 * and whether the router has any VPN clients connected.
                 */
                else if (router.AllowVPNClients && router.HasAnyVPNClientConnected().Result)
                {
                    return false;
                }

                return true; // no exception has been granted
            }

            return false;
        }

        private NetworkRouter? SentByRouter(EthernetPacket packet)
        {
            return Network.OfType<NetworkRouter>().Where(router => router.HasAddress(ip: packet.FindSourceIPAddress())).FirstOrDefault();
        }
    }
}
