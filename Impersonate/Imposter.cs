using MadWizard.ARPergefactor.Impersonate.Methods;
using MadWizard.ARPergefactor.Neighborhood;
using MadWizard.ARPergefactor.Neighborhood.Filter;
using MadWizard.ARPergefactor.Reachability;
using MadWizard.ARPergefactor.Reachability.Events;
using Microsoft.Extensions.Logging;
using PacketDotNet;

using Timer = System.Timers.Timer;

namespace MadWizard.ARPergefactor.Impersonate
{
    public class Imposter
    {
        public required ILogger<Imposter> Logger { private get; init; }

        public required Network Network { private get; init; }
        public required NetworkWatchHost Host { private get; init; }

        public required ImpersonationService Service { private get; init; }
        public required ReachabilityService Reachability { private get; init; }

        private Timer? _preemptiveTimer;
        private ImpersonationRequest? _preemptiveRequest;

        internal void ConfigurePreemptive(TimeSpan interval)
        {
            using var scope = Logger.BeginHostScope(Host);

            List<string> triggers = ["Network"];

            Network.MonitoringStarted += Network_MonitoringStarted;
            Network.MonitoringStopped += Network_MonitoringStopped;

            if (interval > TimeSpan.Zero)
            {
                triggers.Add($"Timer[{interval}]");

                _preemptiveTimer = new Timer(interval);
                _preemptiveTimer.Elapsed += Timer_Elapsed;
                _preemptiveTimer.AutoReset = true;
            }

            triggers.Add($"Unmagic Packet");

            Host.Unseen += Host_Unseen;
            Host.AddressAdded += Host_AddressAdded;
            Host.AddressRemoved += Host_AddressRemoved;
            Host.Seen += Host_Seen;

            Logger.LogDebug($"Impersonation of '{Host.Name}' will be triggered by: {string.Join(", ", triggers)}");
        }

        #region Lifecycle events
        private void Network_MonitoringStarted(object? sender, EventArgs args)
        {
            Timer_Elapsed(sender, args);
        }

        private async void Timer_Elapsed(object? sender, EventArgs args)
        {
            using var scope = Logger.BeginHostScope(Host);

            if (_preemptiveRequest == null)
            {
                Logger.LogTrace($"Should '{Host.Name}' be impersonated? [trigger = {(sender?.GetType().Name)}]");

                _preemptiveRequest = await MaybeImpersonate();
            }
        }

        private async void Host_Unseen(object? sender, EventArgs e)
        {
            using var scope = Logger.BeginHostScope(Host);

            if (Host.PoseMethod is PoseMethod pose && pose.Latency is TimeSpan)
            {
                await Task.Delay(pose.Timeout); // wait for the host to become unreachable

                if (_preemptiveRequest == null && (_preemptiveTimer?.Enabled ?? true))
                {
                    Logger.LogTrace($"Should '{Host.Name}' be impersonated? [trigger = Unmagic Packet]");

                    _preemptiveRequest = await MaybeImpersonate();
                }
            }
        }

        private void Host_AddressAdded(object? sender, AddressEventArgs args)
        {
            using var scope = Logger.BeginHostScope(Host);

            _preemptiveRequest?.AddAddress(args.IPAddress);
        }

        private void Host_AddressRemoved(object? sender, AddressEventArgs args)
        {
            using var scope = Logger.BeginHostScope(Host);

            _preemptiveRequest?.RemoveAddress(args.IPAddress);
        }

        private void Host_Seen(object? sender, EventArgs args)
        {
            using var scope = Logger.BeginHostScope(Host);

            if (_preemptiveRequest != null)
            {
                Logger.LogTrace($"Host '{Host.Name}' has been seen! Stop impersonation immediately...");

                _preemptiveRequest?.Dispose(silently: true);
                _preemptiveRequest = null;
            }
        }

        private void Network_MonitoringStopped(object? sender, EventArgs args)
        {
            using var scope = Logger.BeginHostScope(Host);

            _preemptiveTimer?.Stop();

            _preemptiveRequest?.Dispose(silently: false);
            _preemptiveRequest = null;
        }
        #endregion

        /// <summary>
        /// Handles the automatic impersonation of the host, if it is unreachable.
        /// 
        /// For this to happen, the host must be configured with a pose interval,
        /// and one of the following conditions meet:
        /// 1.) the latency timer elapses
        /// 2.) the host sends a WOL packet, with it's own MAC address, which notifies us about it's scheduled abscence
        /// 
        /// Then we check if the host is still reachable, by sending a ping to all of it's known IP addresses,
        /// before actually starting to impersonate.
        /// </summary>
        private async Task<ImpersonationRequest?> MaybeImpersonate()
        {
            _preemptiveTimer?.Stop(); // stop timer to avoid re-entrance

            try
            {
                /**
                 * We need to make sure, that none of the known IP addresses of the host is reachable,
                 * before we may attempt to impersonate any of them.
                 */
                try
                {
                    var latency = await Reachability.Send(new HostReachabilityTest(Host));

                    Logger.LogTrace($"Impersonation of \"{Host.Name}\" is not needed, as it responded after {latency.TotalMilliseconds} ms.");

                    return null;
                }
                catch (HostTimeoutException ex)
                {
                    Logger.LogDebug($"Received NO response from '{Host.Name}' after {ex.Timeout.TotalMilliseconds} ms");

                    return Impersonate(); // until further notice
                }
            }
            finally
            {
                _preemptiveTimer?.Start();
            }
        }

        public ImpersonationRequest Impersonate(EthernetPacket? trigger = null)
        {
            Logger.LogTrace($"Impersonation of '{Host.Name}' requested...");

            bool advertise = false;
            ImpersonationRequest request;
            if (trigger?.Extract<ArpPacket>() is ArpPacket arp && arp.Operation == ArpOperation.Request)
            {
                request = new(arp.TargetProtocolAddress);
            }
            else if (trigger?.Extract<NdpNeighborSolicitationPacket>() is NdpNeighborSolicitationPacket ndp)
            {
                request = new(ndp.TargetAddress);
            }
            else
            {
                request = new([.. Host.IPAddresses]);

                advertise = true;
            }

            Service.Impersonate(request, advertise);

            if (trigger != null)
            {
                ((INetworkService)Service).ProcessPacket(trigger); // reevaluate packet, to send ARP/NDP response immediately
            }

            return request;
        }
    }
}
