using Autofac;
using Autofac.Core.Lifetime;
using MadWizard.ARPergefactor.Config;
using MadWizard.ARPergefactor.Impersonate;
using MadWizard.ARPergefactor.Logging;
using MadWizard.ARPergefactor.Neighborhood;
using MadWizard.ARPergefactor.Request;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PacketDotNet;
using PacketDotNet.Utils;
using SharpPcap;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Channels;

namespace MadWizard.ARPergefactor
{
    /**
     * https://en.wikipedia.org/wiki/Knocker-up
     */
    internal class KnockerUp : IHostedService
    {
        public required WakeLogger WakeLogger { private get; init; }
        public required ILogger<KnockerUp> Logger { private get; init; }

        public required Lazy<IEnumerable<Network>> Networks { private get; init; }

        readonly ConcurrentDictionary<NetworkHost, WakeRequest> _ongoingRequests = [];

        async Task IHostedService.StartAsync(CancellationToken cancellationToken)
        {
            foreach (var network in Networks.Value)
            {
                network.StartMonitoring();
            }
        }

        public async void MakeHostAvailable(NetworkHost host, EthernetPacket trigger)
        {
            ILifetimeScope scope;

            lock (this)
            {
                if (_ongoingRequests.TryGetValue(host, out WakeRequest? ongoing))
                {
                    ongoing.EnqueuePacket(trigger); return; // save for sequential processing
                }

                if (host.WakeMethod is WakeMethod wake && host.WasSeenSince(wake.Latency))
                {
                    return; // host was seen lately, don't even start a request
                }

                scope = host.StartRequest(trigger, out var request);

                _ongoingRequests[host] = request;
            }

            using (scope) 
            {
                WakeRequest request = _ongoingRequests[host];
                using (Logger.BeginScope(request))
                {
                    Logger.LogTrace($"BEGIN {request}; trigger = \n{trigger.ToTraceString()}");

                    Stopwatch watch = Stopwatch.StartNew();

                    try
                    {
                        await ProcessWakeRequest(request);
                    }
                    catch (Exception ex)
                    {
                        await WakeLogger.LogRequestError(request, ex);
                    }
                    finally
                    {
                        _ongoingRequests.Remove(host, out _);

                        Logger.LogTrace($"END {request}; duration = {watch.ElapsedMilliseconds} ms");
                    }
                }
            }
        }

        private async Task ProcessWakeRequest(WakeRequest request)
        {
            bool shouldSend = false;
            if (!await request.CheckReachability())
                try
                {
                    shouldSend = request.Verify(request.TriggerPacket);
                }
                catch (IPUnicastTrafficNeededException)
                {
                    if (request.Host.PoseMethod?.Timeout is TimeSpan timeout && timeout > TimeSpan.Zero)
                    {
                        using ImpersonationContext ctx = request.Impersonate();

                        await foreach (var packet in request.ReadPackets(timeout))
                        {
                            // we only care about IP packets now
                            if (packet.Extract<IPPacket>() == null)
                                continue; // so we can skip the rest

                            Logger.LogTrace($"CONTINUE with {request}; packet = \n{packet.ToTraceString()}");

                            if (request.Verify(packet))
                            {
                                shouldSend = true; break; // packet qualifies for wake, stop reading
                            }
                        }
                    }

                    else throw;
                }

            if (shouldSend)
            {
                try
                {
                    if (await request.Host.WakeTarget.WakeUp() is bool sent)
                    {
                        if (request.Host.WakeMethod?.Replay ?? false)
                            request.ForwardPackets();

                        await WakeLogger.LogRequest(request, sent);
                    }
                }
                catch (WakeTimeoutException ex)
                {
                    await WakeLogger.LogRequestTimeout(request, ex.Timeout);
                }
            }
        }

        async Task IHostedService.StopAsync(CancellationToken cancellationToken)
        {
            foreach (var network in Networks.Value)
            {
                network.StopMonitoring();
            }
        }
    }
}
