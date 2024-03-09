using Autofac;
using MadWizard.ARPergefactor.Config;
using MadWizard.ARPergefactor.Filter;
using MadWizard.ARPergefactor.Request;
using MadWizard.ARPergefactor.Trigger;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PacketDotNet;
using PacketDotNet.Utils;
using SharpPcap;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace MadWizard.ARPergefactor
{
    internal class KnockerUp(IOptionsMonitor<WakeConfig> config, NetworkSniffer sniffer) : IStartable
    {
        public static readonly PhysicalAddress PhysicalBroadcastAddress = PhysicalAddress.Parse("FF:FF:FF:FF:FF:FF");

        public required ILogger<KnockerUp> Logger { private get; init; }

        public required IEnumerable<IWakeTrigger> Triggers { private get; init; }
        public required IEnumerable<IWakeRequestFilter> Filters { private get; init; }

        void IStartable.Start()
        {
            sniffer.PacketReceived += Sniffer_PacketReceived;
        }

        private void Sniffer_PacketReceived(object? sender, PacketDotNet.Packet packet)
        {
            try
            {
                if (packet is EthernetPacket ethernet)
                {
                    foreach (var trigger in Triggers) 
                    {
                        WakeRequest? request = trigger.AnalyzeNetworkPacket(config.CurrentValue.Network, ethernet);

                        if (request != null)
                        {
                            request.SourcePhysicalAddress = ethernet.SourceHardwareAddress;

                            string description = request.ToString() + $", triggered by {DetermineTrigger(request)}";

                            LogLevel stdLogLvl = request.TargetHost.Silent ? LogLevel.Debug : LogLevel.Information;

                            if (request.WasObserved)
                            {
                                Logger.Log(stdLogLvl, $"Observed {description}");

                                break;
                            }

                            if (VerifyWakeRequest(request))
                                if (SendMagicPacket(request))
                                    Logger.Log(stdLogLvl, $"{trigger.MethodName} {description}");
                                else
                                    Logger.LogWarning($"Could not {trigger.MethodName} {description}");
                            else
                                Logger.LogDebug($"Filtered {description}");

                            break;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Logger.LogError(e, $"Error while analyzing packet: {packet}");
            }
        }

        private bool VerifyWakeRequest(WakeRequest request)
        {
            if (request.TargetHost.WakeTarget == WakeTarget.None)
                return false;

            foreach (var filter in Filters)
                if (filter.FilterWakeRequest(request))
                    return false;

            return true;
        }

        private bool SendMagicPacket(WakeRequest request)
        {
            if (request.TargetHost.PhysicalAddress != null)
            {
                var wol = new WakeOnLanPacket(request.TargetHost.PhysicalAddress);
                var bytes = new byte[wol.Bytes.Length + sniffer.SessionTag.Length];
                System.Array.Copy(wol.Bytes, bytes, wol.Bytes.Length);

                wol = new WakeOnLanPacket(new ByteArraySegment(bytes))
                {
                    Password = sniffer.SessionTag
                };

                switch (request.TargetHost.WakeLayer)
                {
                    case WakeLayer.Ethernet when sniffer.Device is IInjectionDevice inject:
                    {
                        var source = sniffer.Device!.MacAddress;
                        var target = request.TargetHost.WakeTarget == WakeTarget.Unicast ? request.TargetHost.PhysicalAddress : PhysicalBroadcastAddress;

                        var packet = new EthernetPacket(source, target, EthernetType.WakeOnLan)
                        {
                            PayloadPacket = wol
                        };

                        inject.SendPacket(packet);

                        return true;
                    }

                    case WakeLayer.InterNetwork:
                    {
                        var target = request.TargetHost.WakeTarget == WakeTarget.Unicast && request.TargetHost.IPv4Address != null ? request.TargetHost.IPv4Address : IPAddress.Broadcast;
                        var port = request.TargetHost.WakePort;

                        UdpClient udp = new UdpClient();
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

        private static string DetermineTrigger(WakeRequest request)
        {
            string source = "unknown";
            if (request.SourceIPAddress != null)
                source = request.SourceIPAddress.ToString();
            else if (request.SourcePhysicalAddress != null)
                source = string.Join(":", // format MAC-Address
                    (from z in request.SourcePhysicalAddress.GetAddressBytes() select z.ToString("X2")).ToArray());

            string? name = null;
            // Look at known hosts first
            foreach (var host in request.Network.EnumerateHosts())
                if (host.HasAddress(request.SourceIPAddress) || host.HasAddress(request.SourcePhysicalAddress))
                    name = host.Name;
            // then try to resolve unkown hosts
            if (name == null && request.SourceIPAddress != null)
                try { name = Dns.GetHostEntry(request.SourceIPAddress).HostName.Split('.')[0]; } catch { }

            return source + (name != null ? $" ('{name}')" : "");
        }
    }
}
