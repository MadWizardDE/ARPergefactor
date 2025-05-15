using Autofac;
using Autofac.Features.OwnedInstances;
using MadWizard.ARPergefactor.Impersonate.ARP;
using MadWizard.ARPergefactor.Impersonate.NDP;
using MadWizard.ARPergefactor.Neighborhood;
using MadWizard.ARPergefactor.Neighborhood.Filter;
using Microsoft.Extensions.Logging;
using PacketDotNet;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

using Timer = System.Timers.Timer;

namespace MadWizard.ARPergefactor.Impersonate
{
    internal class Imposter : /*IEthernetListener,*/ IDisposable
    {
        private const int INITIAL_IMPERSONATION_DELAY = 1000 * 5; // 5 seconds

        public required ILogger<Imposter> Logger { private get; init; }
        public required ILifetimeScope Scope { private get; init; }

        public required Lazy<NetworkHost> Host { private get; init; }

        readonly ConcurrentDictionary<IPAddress, Impersonation> _impersonations = [];

        readonly Timer _latencyTimer = new(INITIAL_IMPERSONATION_DELAY);

        public Imposter()
        {
            _latencyTimer.Elapsed += MaybeImpersonate;
            _latencyTimer.AutoReset = false;
            _latencyTimer.Start();
        }

        public bool IsImpersonating(IPAddress? ip = null)
        {
            if (_impersonations.IsEmpty)
                return false;

            return ip == null || _impersonations.ContainsKey(ip);
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
                _latencyTimer.Stop(); // stop timer to avoid re-entrance

                host.Unseen -= MaybeImpersonate;

                Logger.LogTrace($"Checking if {host.Name} can be impersonated...");

                /**
                 * We need to make sure, that no of the known IP addresses of the host is reachable,
                 * before we can attempt to impersonate any of them.
                 */
                List<Task> pings = [];
                foreach (var ip in host.IPAddresses)
                    if (!IsImpersonating(ip))
                        switch (ip.AddressFamily)
                        {
                            case AddressFamily.InterNetwork:
                                pings.Add(host.DoARPing(ip, timeout));
                                break;

                            case AddressFamily.InterNetworkV6:
                                pings.Add(host.DoNDPing(ip, timeout));
                                break;
                        }

                await Task.WhenAll(pings);

                if (host.WasSeenSince(timeout))
                {
                    Logger.LogDebug($"Impersonation of \"{host.Name}\" is not needed, as it is reachable.");
                }
                else
                {
                    Impersonate(); // until further notice
                }

                host.Unseen += MaybeImpersonate; // maybe the host is cooperative and notifies us, before it becomes unreachable

                _latencyTimer.Interval = latency.TotalMilliseconds;
                _latencyTimer.Start();
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

        private T MaybeStartImpersonation<T,P>(IPAddress ip, P? packet = null) where T : Impersonation<P> where P : Packet
        {
            if (_impersonations.Values.OfType<T>().Where(imp => imp.IPAddress.Equals(ip)).FirstOrDefault() is var imp && imp == null)
            {
                var owned = Scope.Resolve<Owned<T>>(new TypedParameter(typeof(IPAddress), ip));

                _impersonations[ip] = imp = owned.Value;

                imp.StartWith(packet);

                imp.PresenceDetected += (sender, args) =>
                {
                    StopImpersonation(true);
                };

                imp.Stopped += (sender, args) =>
                {
                    owned.Dispose();

                    _impersonations.Remove(ip, out _);
                };
            }

            return imp;
        }

        public bool Handle(EthernetPacket packet)
        {
            return _impersonations.Values.Aggregate(false, (filter, imp) => filter || imp.Handle(packet));
        }

        public void StopImpersonation(bool passive = false)
        {
            foreach (var imp in _impersonations.Values.ToArray())
            {
                imp.Stop(passive);
            }
        }

        void IDisposable.Dispose()
        {
            StopImpersonation();
        }
    }
}
