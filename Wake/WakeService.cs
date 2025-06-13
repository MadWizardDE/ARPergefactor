using Autofac;
using MadWizard.ARPergefactor.Impersonate;
using MadWizard.ARPergefactor.Neighborhood;
using MadWizard.ARPergefactor.Neighborhood.Filter;
using MadWizard.ARPergefactor.Reachability;
using MadWizard.ARPergefactor.Wake.Methods;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PacketDotNet;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace MadWizard.ARPergefactor.Wake
{
    internal class WakeService : INetworkService
    {
        public required ILogger<WakeService> Logger { private get; init; }

        public required Network Network { private get; init; }
        public required NetworkDevice Device { private get; init; }

        public required ReachabilityService Reachability { private get; init; }

        public required IEnumerable<IWakeTrigger> Triggers { private get; init; }

        readonly ConcurrentDictionary<NetworkHost, WakeRequest> _ongoingRequests = [];
       
        private int _requestNr = 1;

        void INetworkService.ProcessPacket(EthernetPacket packet)
        {
            if (Network.IsInScope(packet))
            {
                foreach (var trigger in Triggers)
                {
                    if (trigger.Examine(packet, out bool skipFilters) is NetworkWatchHost host)
                    {
                        MakeHostAvailable(host, packet, skipFilters);

                        break;
                    }
                }
            }
        }

        private async void MakeHostAvailable(NetworkWatchHost host, EthernetPacket trigger, bool skipFilters = false)
        {
            if (host.HasBeenSeen(host.WakeMethod.Latency) || host.WakeTarget().HasBeenWokenSince(host.WakeMethod.Latency))
                return; // host was seen lately or waken, don't even start a request

            WakeRequest request; ILifetimeScope scope;

            lock (this)
            {
                if (_ongoingRequests.TryGetValue(host, out WakeRequest? ongoing))
                {
                    ongoing.EnqueuePacket(trigger); return; // save for sequential processing
                }

                (scope, request) = host.StartWakeRequest(trigger);

                request.Number = _requestNr++;
                request.SkipFilters = skipFilters;

                _ongoingRequests[host] = request;
            }

            using (scope) using (Logger.BeginHostScope(request.Host))
            {
                Logger.LogTrace($"BEGIN {request}; trigger = \n{trigger.ToTraceString()}");

                Stopwatch watch = Stopwatch.StartNew();

                try
                {
                    await ProcessWakeRequest(request);
                }
                catch (Exception ex)
                {
                    await Logger.LogRequestError(request, ex);
                }
                finally
                {
                    _ongoingRequests.Remove(host, out _);

                    Logger.LogTrace($"END {request}; duration = {watch.ElapsedMilliseconds} ms");
                }
            }
        }

        private async Task ProcessWakeRequest(WakeRequest request)
        {
            bool shouldSend = false;
            if (request.TriggerPacket.FindDestinationIPAddress() is var ip)
                if (Network.IsImpersonating(ip) || !await Reachability.Test(request.Host, ip))
                    try
                    {
                        request.EnqueuePacket(request.TriggerPacket, true);

                        shouldSend = request.Verify(request.TriggerPacket);
                    }
                    catch (IPUnicastTrafficNeededException)
                    {
                        if (request.Host.PoseMethod.Timeout is TimeSpan timeout && timeout > TimeSpan.Zero)
                        {
                            using ImpersonationRequest imp = request.Impersonate();

                            await foreach (var packet in request.ReadPackets(timeout))
                            {
                                // we only care about IP packets now
                                if (!packet.IsIPUnicast())
                                    continue; // so we can skip the rest

                                Logger.LogTrace($"CONTINUE with {request}; packet = \n{packet.ToTraceString()}");

                                if (request.Verify(packet))
                                {
                                    request.EnqueuePacket(packet, true);

                                    shouldSend = true; break; // packet qualifies for wake, stop reading
                                }
                            }
                        }
                        else
                            throw;
                    }

            if (shouldSend)
            {
                try
                {
                    var latency = await WakeUp(request.Host);

                    if (request.Host.WakeMethod.Forward)
                        request.ForwardPackets();

                    await Logger.LogRequest(request, latency);
                }
                catch (HostTimeoutException ex)
                {
                    await Logger.LogRequestTimeout(request, ex.Timeout);
                }
            }
        }

        public async Task<TimeSpan?> WakeUp(NetworkWatchHost host)
        {
            var stopwatch = Stopwatch.StartNew();

            int countPackets = 0;

            if (host is VirtualWatchHost virt)
            {
                if (!(await Reachability.Test(virt.PhysicalHost)))
                {
                    if ((countPackets += SendMagicPacket(virt.PhysicalHost)) > 0)
                    {
                        await Reachability.Until(virt.PhysicalHost, virt.PhysicalHost.WakeMethod.Timeout);
                    }
                }
            }

            if (host.PhysicalAddress is not null)
            {
                countPackets += SendMagicPacket(host); // always wake up the target host
            }

            if (countPackets > 0)
            {
                await Reachability.Until(host, host.WakeMethod.Timeout);

                return stopwatch.Elapsed;
            }

            return null;
        }

        private int SendMagicPacket(NetworkWatchHost host)
        {
            var wol = new WakeOnLanPacket(host.PhysicalAddress ?? throw new HostAbortedException($"Host '{host.Name}' has no PhysicalAddress configured."));

            Logger.LogTrace($"Wake up '{host.Name}' at {host.PhysicalAddress.ToHexString()}");

            var sourceMAC = Device.PhysicalAddress;
            var targetMAC = host.WakeMethod.Route == WakeTransmissionType.Unicast
                ? host.PhysicalAddress
                : PhysicalAddressExt.Broadcast;

            int countPackets = 0;
            switch (host.WakeMethod.Layer)
            {
                case WakeLayer.Link:
                {
                    Device.SendPacket(new EthernetPacket(sourceMAC, targetMAC, EthernetType.WakeOnLan)
                    {
                        PayloadPacket = wol
                    });

                    host.LastWake = DateTime.Now; 
                    countPackets++;
                    break;
                }

                case WakeLayer.Network:
                {
                    Random rand = new();
                    foreach (var target in host.WakeMethod.Route == WakeTransmissionType.Unicast ? host.IPAddresses : [IPAddress.Broadcast])
                    {
                        IPPacket payloadIP = target.AddressFamily == AddressFamily.InterNetwork
                            ? new IPv4Packet(Device.IPv4Address, target)
                            : new IPv6Packet(Device.IPv6LinkLocalAddress, target);

                        payloadIP.PayloadPacket = new UdpPacket((ushort)rand.Next(49152, 65536), host.WakeMethod.Port)
                        {
                            PayloadPacket = wol
                        };

                        Device.SendPacket(new EthernetPacket(sourceMAC, targetMAC, payloadIP is IPv4Packet ? EthernetType.IPv4 : EthernetType.IPv6)
                        {
                            PayloadPacket = payloadIP
                        });

                        host.LastWake = DateTime.Now;
                        countPackets++;
                    }

                    break;
                }
            }

            return countPackets;
        }
    }
}