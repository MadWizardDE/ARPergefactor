using MadWizard.ARPergefactor.Impersonate;
using MadWizard.ARPergefactor.Neighborhood.Filter;
using MadWizard.ARPergefactor.Neighborhood.Tables;
using Nito.AsyncEx;
using PacketDotNet;
using System.Net;

namespace MadWizard.ARPergefactor.Neighborhood
{
    public class Network(NetworkOptions options)
    {
        public readonly HostTable Hosts = new();

        public NetworkOptions Options => options;

        public required NetworkDevice Device { private get; init; }

        public IEnumerable<INetworkService> Services { private get; init; } = [];

        public AsyncLock Lock { get; } = new();

        public event EventHandler? MonitoringStarted;
        public event EventHandler? MonitoringStopped;

        public void AddHost(NetworkHost host, TimeSpan? lifetime = null)
        {
            if (!(lifetime != null ? Hosts.SetDynamicEntry(host, lifetime.Value) : Hosts.AddStaticEntry(host)))
            {
                throw new ArgumentException($"Host '{host.Name}' already exists on network '{Device.Name}'.");
            }
        }

        public bool IsInScope(EthernetPacket packet)
        {
            return Options.WatchScope == WatchScope.Network
                || Options.WatchScope == WatchScope.Host && Device.HasSentPacket(packet);
        }

        public bool IsImpersonating(IPAddress? ip)
        {
            ImpersonationService? service = Services.OfType<ImpersonationService>().FirstOrDefault();

            return ip != null && (service?.IsImpersonating(ip) ?? false);
        }

        public void StartMonitoring()
        {
            Device.EthernetCaptured += HandlePacket;
            Device.StartCapture();

            foreach (var service in Services)
            {
                service.Startup();
            }

            MonitoringStarted?.Invoke(this, EventArgs.Empty);
        }

        private void HandlePacket(object? sender, EthernetPacket packet)
        {
            using (Lock.Lock())
            {
                foreach (var service in Services)
                {
                    service.ProcessPacket(packet);
                }
            }
        }

        public void StopMonitoring()
        {
            MonitoringStopped?.Invoke(this, EventArgs.Empty);

            foreach (var service in Services)
            {
                service.Shutdown();
            }

            Device.StopCapture();
            Device.EthernetCaptured -= HandlePacket;
        }
    }
}
