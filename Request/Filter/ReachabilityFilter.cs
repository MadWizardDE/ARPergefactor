using MadWizard.ARPergefactor;
using MadWizard.ARPergefactor.Config;
using MadWizard.ARPergefactor.Neighborhood;
using MadWizard.ARPergefactor.Request;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PacketDotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MadWizard.ARPergefactor.Request.Filter
{
    internal class ReachabilityFilter : IWakeRequestFilter
    {
        public required ILogger<ReachabilityFilter> Logger { private get; init; }

        public required NetworkHost Host { private get; init; }

        bool needToCheck = true;

        async Task<bool?> IWakeRequestFilter.ShouldFilterPacket(EthernetPacket packet)
        {
            if (ShouldPing() is TimeSpan timeout)
            {
                if (!Host.WasSeenSince(timeout))
                {
                    Logger.LogTrace($"Checking reachability of '{Host.Name}'...");

                    try
                    {
                        TimeSpan latency;
                        if (packet.FindDestinationIPAddress() is IPAddress ip)
                        {
                            switch (ip.AddressFamily)
                            {
                                case AddressFamily.InterNetwork:
                                    latency = await Host.DoARPing(ip, timeout);
                                    break;

                                case AddressFamily.InterNetworkV6:
                                    latency = await Host.DoNDPing(ip, timeout);
                                    break;

                                default:
                                    throw new Exception($"Unsupported address family {ip.AddressFamily} for {Host.Name}");
                            }
                        }
                        else
                        {
                            latency = await Host.SendICMPEchoRequest(timeout);
                        }

                        Logger.LogDebug($"Received response from '{Host.Name}' after {Math.Ceiling(latency.TotalMilliseconds)} ms");
                    }
                    catch (TimeoutException)
                    {
                        Logger.LogDebug($"Received NO response from '{Host.Name}' after {timeout.TotalMilliseconds} ms");

                        return needToCheck = false; // host is most probably offline
                    }
                }

                Logger.LogTrace($"Received last response from '{Host.Name}' since {(DateTime.Now - Host.LastSeen)?.TotalMilliseconds} ms");

                return true; // host was seen lately
            }

            return false;
        }

        private TimeSpan? ShouldPing()
        {
            return needToCheck && Host.PingMethod is PingMethod method ? method.Timeout : null;
        }
    }
}
