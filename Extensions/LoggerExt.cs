using MadWizard.ARPergefactor.Neighborhood;
using MadWizard.ARPergefactor.Wake;
using MadWizard.ARPergefactor.Wake.Filter.Rules;
using Microsoft.Extensions.Logging;
using NLog;
using PacketDotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Extensions.Logging
{
    public static class LoggerExt
    {
        public static IDisposable? BeginHostScope(this ILogger logger, NetworkHost networkHost)
        {
            return logger.BeginScope(new Dictionary<string, object> { ["HostName"] = networkHost.Name });
        }

        public static async Task LogFilteredRequest(this ILogger logger, WakeRequest request, IWakeFilter filter)
        {
            await logger.LogFilteredRequest(request, filter.GetType().Name);
        }

        public static async Task LogFilteredRequest(this ILogger logger, WakeRequest request, string filter)
        {
            //if (filter.Length > 0)
            //    Logger.LogTrace($"Filtered {await request.ToDescription()} -> {filter}");
            //else
            //    Logger.LogTrace($"Filtered {await request.ToDescription()}");
            if (filter.Length > 0)
                logger.LogTrace($"Filtered {request} -> {filter}");
            else
                logger.LogTrace($"Filtered {request}");
        }

        public static async Task LogRequestError(this ILogger logger, WakeRequest request, Exception ex)
        {
            await logger.LogEvent(LogLevel.Error, $"Error processing {request};", request.Host, request.TriggerPacket, request.Service, ex);
        }

        internal static async Task LogRequestTimeout(this ILogger logger, WakeRequest request, TimeSpan timeout)
        {
            await logger.LogEvent(LogLevel.Warning, "Timeout at", request.Host, request.TriggerPacket, request.Service, latency: timeout);
        }

        public static async Task LogRequest(this ILogger logger, WakeRequest request, TimeSpan? latency)
        {
            if (latency is TimeSpan duration)
                await logger.LogEvent(null, request.ToMethod(), request.Host, request.TriggerPacket, request.Service, latency: duration);
            else
                await logger.LogEvent(LogLevel.Warning, "Could not " + request.ToMethod(), request.Host, request.TriggerPacket, request.Service);
        }

        public static async Task LogEvent(this ILogger logger, LogLevel? level, string method, NetworkWatchHost host, EthernetPacket? trigger = null, TransportService? service = null, Exception? ex = null, TimeSpan? latency = null)
        {
            string description = host.ToTarget();

            if (service != null)
            {
                description += $", using {service.Value.ToDescription()}";
            }

            if (trigger != null)
            {
                description += $", triggered by {await host.Network.ToTrigger(trigger)}";
            }

            if (latency != null)
            {
                description += $" [{Math.Floor(latency.Value.TotalMilliseconds)} ms]";
            }

            logger.Log(level ?? host.ToLevel(), ex, $"{method} {description}");
        }
    }

    file static class LogHelper
    {
        public static string ToMethod(this WakeRequest request)
        {
            if (request.TriggerPacket.Extract<WakeOnLanPacket>() is not null)
                return "Rerouted";

            return "Send";
        }

        public static LogLevel ToLevel(this NetworkWatchHost host)
        {
            return host.WakeMethod.Silent ? LogLevel.Debug : LogLevel.Information;
        }

        public static async Task<string> ToTrigger(this WakeRequest request)
        {
            return await ToTrigger(request.Host.Network, request.TriggerPacket);
        }

        public static string ToDescription(this TransportService service)
        {
            return $"{service.Port}/{service.ProtocolType.ToString().ToLower()} (\"{service.Name}\")";
        }

        public static async Task<string> ToTrigger(this Network network, EthernetPacket packet)
        {
            PhysicalAddress? sourceMac = packet.FindSourcePhysicalAddress();
            IPAddress? sourceIP = packet.FindSourceIPAddress();

            string source = "unknown";
            if (sourceIP != null)
                source = sourceIP.ToString();
            else if (sourceMac != null)
                source = sourceMac.ToHexString();

            string? name = null;

            // Look at known hosts first
            foreach (var host in network.Hosts)
            {
                if (host.HasAddress(sourceMac, sourceIP))
                {
                    if (host is NetworkRouter router && router.FindVPNClient(sourceIP) is NetworkHost vpn)
                    {
                        name = vpn.Name;
                    }
                    else
                    {
                        name = host.Name;
                    }

                    break;
                }
            }

            // then try to resolve unkown hosts
            if (name == null && sourceIP != null)
                try { name = (await Dns.GetHostEntryAsync(sourceIP)).HostName.Split('.')[0]; } catch { } // TODO only remove, if it has the local DNS suffix

            return source + (name != null ? $" (\"{name}\")" : "");
        }


        public static string ToTarget(this NetworkHost host)
        {
            if (host is VirtualWatchHost virt)
            {
                return ($"Magic Packet for \"{host.Name}\" to \"{virt.PhysicalHost.Name}\"");
            }
            else
            {
                return ($"Magic Packet to \"{host.Name}\"");
            }
        }

        public static async Task<string> ToDescription(this WakeRequest request)
        {
            return $"{request.Host.ToTarget()}, triggered by {await request.Host.Network.ToTrigger(request.TriggerPacket)}";
        }
    }
}
