using ARPergefactor.Packet;
using Autofac;
using MadWizard.ARPergefactor.Config;
using MadWizard.ARPergefactor.Filter;
using MadWizard.ARPergefactor.Packets;
using MadWizard.ARPergefactor.Request;
using MadWizard.ARPergefactor.Trigger;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PacketDotNet;
using PacketDotNet.Utils;
using SharpPcap;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Channels;

namespace MadWizard.ARPergefactor
{
    internal class KnockerUp(IOptionsMonitor<WakeConfig> config, NetworkSniffer sniffer) : BackgroundService
    {
        public required ILogger<KnockerUp> Logger { private get; init; }

        public required IEnumerable<IWakeTrigger> Triggers { private get; init; }
        public required IEnumerable<IWakeRequestFilter> Filters { private get; init; }

        private readonly Channel<WakeRequest> _requestChannel = Channel.CreateUnbounded<WakeRequest>();

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            sniffer.PacketReceived += Sniffer_PacketReceived;

            try
            {
                while (await _requestChannel.Reader.ReadAsync(stoppingToken) is WakeRequest request)
                    try
                    {
                        bool sent = false;
                        if (!request.IsExternal)
                        {
                            if (await VerifyWakeRequest(request))
                            {
                                if (sent = SendMagicPacket(request))
                                {
                                    request.TargetHost.LastWake = DateTime.Now;
                                }
                            }
                        }

                        LogMagicPacketEvent(request, sent);
                    }
                    catch (Exception e)
                    {
                        Logger.LogError(e, $"Error while verifying request: {request}");
                    }
            }
            catch (OperationCanceledException)
            {
                sniffer.PacketReceived -= Sniffer_PacketReceived;
            }
        }

        private void Sniffer_PacketReceived(object? sender, Packet packet)
        {
            try
            {
                if (packet is EthernetPacket ethernet)
                    foreach (var trigger in Triggers)
                    {
                        WakeRequest? request = trigger.AnalyzeNetworkPacket(config.CurrentValue.Network, ethernet);

                        if (request != null)
                        {
                            request.TriggerPacket = ethernet;
                            request.SendMethod = trigger.MethodName;

                            _requestChannel.Writer.TryWrite(request);

                            break;
                        }
                    }
            }
            catch (Exception e)
            {
                Logger.LogError(e, $"Error while analyzing packet: {packet}");
            }
        }

        private async Task<bool> VerifyWakeRequest(WakeRequest request)
        {
            if (request.TargetHost.WakeTarget == WakeTarget.None)
                return false;

            if (request.TargetHost.LastWake != null)
                if ((DateTime.Now - request.TargetHost.LastWake).Value.TotalMilliseconds < config.CurrentValue.ThrottleTimeout)
                {
                    request.FilteredBy = "Throttle";

                    return false;
                }

            foreach (var filter in Filters)
                if (await filter.FilterWakeRequest(request))
                {
                    request.FilteredBy = filter.GetType().Name;

                    return false;
                }

            return true;
        }

        private bool SendMagicPacket(WakeRequest request)
        {
            if (request.TargetHost.PhysicalAddress != null)
            {
                var wol = new WakeOnLanPacket(request.TargetHost.PhysicalAddress);

                switch (request.TargetHost.WakeLayer)
                {
                    case WakeLayer.Ethernet when sniffer.PhysicalAddress is not null:
                    {
                        var source = sniffer.PhysicalAddress;
                        var target = request.TargetHost.WakeTarget == WakeTarget.Unicast ? request.TargetHost.PhysicalAddress : PhysicalAddressExt.Broadcast;

                        var packet = new EthernetPacket(source, target, EthernetType.WakeOnLan)
                        {
                            PayloadPacket = wol
                        };

                        sniffer.SendPacket(packet);

                        return true;
                    }

                    case WakeLayer.InterNetwork:
                    {
                        var target = request.TargetHost.WakeTarget == WakeTarget.Unicast && request.TargetHost.IPv4Address != null ? request.TargetHost.IPv4Address : IPAddress.Broadcast;
                        var port = request.TargetHost.WakePort;

                        UdpClient udp = new();
                        if (request.TargetHost.WakeTarget == WakeTarget.Broadcast)
                        {
                            udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, 1);
                            udp.EnableBroadcast = true;
                        }

                        udp.Send(wol.Bytes, new IPEndPoint(target, port));

                        return true;
                    }
                }
            }

            return false;
        }

        #region Logging
        private async void LogMagicPacketEvent(WakeRequest request, bool sent)
        {
            LogLevel stdLogLvl = request.TargetHost.Silent ? LogLevel.Debug : LogLevel.Information;

            string description = request.ToString() + $", triggered by {await DetermineTrigger(request)}";

            if (request.IsExternal)
                Logger.Log(stdLogLvl, $"Observed {description}");
            else if (request.FilteredBy != null)
            {
                if (request.FilteredBy.Length > 0)
                    Logger.LogTrace($"Filtered {description} -> {request.FilteredBy}");
                else
                    Logger.LogTrace($"Filtered {description}");
            }
            else if (!sent)
                Logger.LogWarning($"Could not {request.SendMethod} {description}");
            else
                Logger.Log(stdLogLvl, $"{request.SendMethod} {description}");
        }

        private static async Task<string> DetermineTrigger(WakeRequest request)
        {
            string source = "unknown";
            if (request.SourceIPAddress != null)
                source = request.SourceIPAddress.ToString();
            else if (request.SourcePhysicalAddress != null)
                source = request.SourcePhysicalAddress.ToHexString();

            string? name = null;
            // Look at known hosts first
            foreach (var host in request.Network.EnumerateHosts())
                if (host.HasAddress(request.SourceIPAddress) || host.HasAddress(request.SourcePhysicalAddress))
                    name = host.Name;
            // then try to resolve unkown hosts
            if (name == null && request.SourceIPAddress != null)
                try { name = (await Dns.GetHostEntryAsync(request.SourceIPAddress)).HostName.Split('.')[0]; } catch { }

            return source + (name != null ? $" (\"{name}\")" : "");
        }
        #endregion
    }
}
