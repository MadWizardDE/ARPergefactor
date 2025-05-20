using MadWizard.ARPergefactor.Neighborhood;
using MadWizard.ARPergefactor.Request;
using MadWizard.ARPergefactor.Request.Filter.Rules;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PacketDotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.ARPergefactor.Logging
{
    public class WakeLogger
    {
        public required ILogger<WakeLogger> Logger { private get; init; }

        public async Task LogFilteredRequest(WakeRequest request, IWakeRequestFilter filter)
        {
            await LogFilteredRequest(request, filter.GetType().Name);
        }

        public async Task LogFilteredRequest(WakeRequest request, string filter)
        {
            //if (filter.Length > 0)
            //    Logger.LogTrace($"Filtered {await request.ToDescription()} -> {filter}");
            //else
            //    Logger.LogTrace($"Filtered {await request.ToDescription()}");
            if (filter.Length > 0)
                Logger.LogTrace($"Filtered {request} -> {filter}");
            else
                Logger.LogTrace($"Filtered {request}");
        }

        public async Task LogRequestError(WakeRequest request, Exception ex)
        {
            await LogEvent(LogLevel.Error, $"Error processing {request};", request.Host, request.TriggerPacket, request.Service, ex);
        }

        internal async Task LogRequestTimeout(WakeRequest request, TimeSpan timeout)
        {
            await LogEvent(LogLevel.Warning, "Timeout at", request.Host, request.TriggerPacket, request.Service, latency:timeout);
        }

        public async Task LogRequest(WakeRequest request, bool sent)
        {
            if (sent)
            {
                var time = request.Host.WakeTarget.LastSeen - request.Host.WakeTarget.LastWake;

                await LogEvent(null, request.ToMethod(), request.Host, request.TriggerPacket, request.Service, latency:time);
            }
            else
                await LogEvent(LogLevel.Warning, "Could not " + request.ToMethod(), request.Host, request.TriggerPacket, request.Service);
        }

        public async Task LogEvent(LogLevel? level, string method, NetworkHost host, EthernetPacket? trigger = null, TransportService? service = null, Exception? ex = null, TimeSpan? latency = null)
        {
            string description = host.ToTarget();

            if (service != null)
            {
                description += $", requested {service.Value.ToDescription()}";
            }

            if (trigger != null)
            {
                description += $", triggered by {await host.Network.ToTrigger(trigger)}";
            }

            if (latency != null)
            {
                description += $" [{Math.Floor(latency.Value.TotalMilliseconds)} ms]";
            }

            Logger.Log(level ?? host.ToLevel(), ex, $"{method} {description}");
        }
    }

    file static class LogHelper
    {
        public static string ToMethod(this WakeRequest request)
        {
            return "Send";
        }

        public static LogLevel ToLevel(this NetworkHost host)
        {
            return host.WakeTarget.WakeMethod?.Silent ?? false ? LogLevel.Debug : LogLevel.Information;
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
            foreach (var host in network)
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
                try { name = (await Dns.GetHostEntryAsync(sourceIP)).HostName.Split('.')[0]; } catch { }

            return source + (name != null ? $" (\"{name}\")" : "");
        }


        public static string ToTarget(this NetworkHost host)
        {
            if (host != host.WakeTarget)
            {
                return ($"Magic Packet to \"{host.WakeTarget.Name}\" for \"{host.Name}\"");
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
