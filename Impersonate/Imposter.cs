using Autofac;
using Autofac.Features.OwnedInstances;
using MadWizard.ARPergefactor.Impersonate.ARP;
using MadWizard.ARPergefactor.Impersonate.NDP;
using MadWizard.ARPergefactor.Neighborhood;
using MadWizard.ARPergefactor.Neighborhood.Filter;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PacketDotNet;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

using Timer = System.Timers.Timer;

namespace MadWizard.ARPergefactor.Impersonate
{
    public class Imposter
    {
        private const int WAIT_IMPERSONATION_DELAY = 1000 * 5; // 5 seconds

        public required ILogger<Imposter> Logger { private get; init; }
        public required ILifetimeScope Scope { private get; init; }

        public required Network Network { private get; init; }
        public required Lazy<NetworkHost> Host { private get; init; }

        private Timer? _latencyTimer;

        internal void ConfigureImpersonation()
        {
            Host.Value.AddressRemoved += (sender, args) => StopImpersonation(args.IP);
            Host.Value.Seen += (sender, args) => StopImpersonation();
        }

        internal void ConfigurePreemptiveImpersonation(TimeSpan interval)
        {
            if (interval > TimeSpan.Zero)
            {
                _latencyTimer = new Timer(interval.TotalMilliseconds);
                _latencyTimer.Elapsed += MaybeImpersonate;
                _latencyTimer.AutoReset = false;

                Host.Value.AddressAdded += MaybeImpersonate;
            }

            Network.MonitoringStarted += MaybeImpersonate;
        }

        /// <summary>
        /// Handles the automatic impersonation of the host, if it is unreachable.
        /// 
        /// For this to happen, the host must be configured with a pose interval,
        /// and one of the following conditions meet:
        /// 1.) the latency timer elapses
        /// 2.) the host sends us a gratuitous ARP or NDP packet, notify us about it's schedules abscence
        /// 
        /// Then we check if the host is still reachable, by sending a ping to all of it's known IP addresses,
        /// before actually starting to impersonate.
        /// </summary>
        private async void MaybeImpersonate(object? sender, EventArgs args)
        {
            // TODO we may get a (small) race condition here

            if (Host.Value is NetworkHost host
                && host.PoseMethod?.Latency is TimeSpan latency
                && host.PingMethod?.Timeout is TimeSpan timeout)
            {
                _latencyTimer?.Stop(); // stop timer to avoid re-entrance

                host.Unseen -= MaybeImpersonate;

                if (sender is NetworkHost)
                {
                    await Task.Delay(WAIT_IMPERSONATION_DELAY); // wait for the host to become unreachable
                }

                Logger.LogTrace($"Checking if {host.Name} should be impersonated...");

                /**
                 * We need to make sure, that no of the known IP addresses of the host is reachable,
                 * before we can attempt to impersonate any of them.
                 */
                List<Task> pings = [];
                foreach (var ip in host.IPAddresses)
                    if (!Network.IsImpersonating(ip))
                        switch (ip.AddressFamily)
                        {
                            case AddressFamily.InterNetwork:
                                pings.Add(host.DoARPing(ip, timeout));
                                break;

                            case AddressFamily.InterNetworkV6:
                                pings.Add(host.DoNDPing(ip, timeout));
                                break;
                        }

                if (pings.Count > 0)
                {
                    try { await Task.WhenAll(pings); } catch (TimeoutException) { /* ignore here */ }

                    if (host.WasSeenSince(timeout))
                    {
                        Logger.LogTrace($"Impersonation of \"{host.Name}\" is not needed, as it is reachable.");
                    }
                    else
                    {
                        Impersonate(); // until further notice
                    }
                }

                host.Unseen += MaybeImpersonate; // maybe the host is cooperative and notifies us, before it becomes unreachable

                _latencyTimer?.Start();
            }
        }

        public ImpersonationContext Impersonate(EthernetPacket? trigger = null)
        {
            Logger.LogTrace($"Impersonation of {Host.Value.Name} requested...");

            ImpersonationContext request = new();

            if (trigger?.Extract<ArpPacket>() is ArpPacket arp && arp.Operation == ArpOperation.Request)
            {
                request.AddReferenceTo(MaybeStartImpersonation<ARPImpersonation, ArpPacket>(arp.TargetProtocolAddress, arp));
            }
            else if (trigger?.Extract<NdpPacket>() is NdpPacket ndp)
            {
                // TODO implement NDP
                //request.AddReferenceTo(StartImpersonation<NDPImpersonation, NdpPacket>(ndp));
            }
            else foreach (var ip in Host.Value.IPAddresses)
                    switch (ip.AddressFamily)
                    {
                        case AddressFamily.InterNetwork:
                            request.AddReferenceTo(MaybeStartImpersonation<ARPImpersonation, ArpPacket>(ip));
                            break;
                        case AddressFamily.InterNetworkV6:
                            request.AddReferenceTo(MaybeStartImpersonation<NDPImpersonation, NdpPacket>(ip));
                            break;
                    }

            return request;
        }

        private T MaybeStartImpersonation<T, P>(IPAddress ip, P? packet = null) where T : Impersonation<P> where P : Packet
        {
            if (!Network.IsImpersonating(ip, out Impersonation? imp))
            {
                var owned = Scope.Resolve<Owned<T>>(new TypedParameter(typeof(IPAddress), ip));

                Network.RegisterImpersonation(imp = owned.Value);

                owned.Value.StartWith(packet);

                imp.Stopped += (sender, args) =>
                {
                    owned.Dispose();
                };
            }

            return (imp as T)!;
        }

        private void StopImpersonation(IPAddress? adr = null)
        {
            foreach (var ip in adr != null ? [adr] : Host.Value.IPAddresses)
                if (Network.IsImpersonating(ip, out Impersonation? imp))
                    imp?.Stop(true);
        }
    }
}
