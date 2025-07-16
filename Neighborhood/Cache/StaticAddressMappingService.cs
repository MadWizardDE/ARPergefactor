using MadWizard.ARPergefactor.Neighborhood.Filter;
using MadWizard.ARPergefactor.Reachability.Events;
using Microsoft.Extensions.Logging;
using PacketDotNet;
using System.Net.NetworkInformation;

namespace MadWizard.ARPergefactor.Neighborhood.Cache
{
    public class StaticAddressMappingService : INetworkService
    {
        public required ILogger<StaticAddressMappingService> Logger { private get; init; }

        public required ILocalIPCache Cache { private get; init; }

        public required Network Network { private get; init; }

        void INetworkService.Startup()
        {
            Logger.LogDebug("Installing static address mappings...");

            foreach (var host in Network.Hosts)
            {
                host.AddressAdded += Host_AddressAdded;
                host.PhysicalAddressChanged += Host_PhysicalAddressChanged;
                host.AddressRemoved += Host_AddressRemoved;

                if (host.PhysicalAddress is PhysicalAddress mac)
                    foreach (var ip in host.IPAddresses)
                        if (Network.IsInLocalSubnet(ip))
                            Cache.Update(ip, mac);
            }
        }

        void INetworkService.ProcessPacket(EthernetPacket packet)
        {
            // TODO maybe update static mappings based on received packets?
        }

        void INetworkService.Shutdown()
        {
            Logger.LogDebug("Deleting static address mappings...");

            foreach (var host in Network.Hosts)
            {
                host.AddressAdded -= Host_AddressAdded;
                host.PhysicalAddressChanged -= Host_PhysicalAddressChanged;
                host.AddressRemoved -= Host_AddressRemoved;

                foreach (var ip in host.IPAddresses)
                    if (Network.IsInLocalSubnet(ip))
                        if (host.PhysicalAddress is not null)
                            Cache.Delete(ip);
            }
        }

        #region NetworkHost events
        private void Host_AddressAdded(object? sender, AddressEventArgs args)
        {
            if (sender is NetworkHost host && host.PhysicalAddress is PhysicalAddress mac)
            {
                Logger.LogDebug($"Updating static address mappings for host '{host.Name}'...");

                if (Network.IsInLocalSubnet(args.IPAddress))
                    Cache.Update(args.IPAddress, mac);
            }
        }

        private void Host_PhysicalAddressChanged(object? sender, PhysicalAddressEventArgs args)
        {
            if (sender is NetworkHost host)
            {
                Logger.LogDebug($"Updating static address mappings for host '{host.Name}'...");

                foreach (var ip in host.IPAddresses)
                {
                    if (Network.IsInLocalSubnet(ip))
                    {
                        Cache.Update(ip, args.PhysicalAddress);
                    }
                }
            }
        }

        private void Host_AddressRemoved(object? sender, AddressEventArgs args)
        {
            if (sender is NetworkHost host && host.PhysicalAddress is not null)
            {
                Logger.LogDebug($"Updating static address mappings for host '{host.Name}'...");

                if (Network.IsInLocalSubnet(args.IPAddress))
                    Cache.Delete(args.IPAddress);
            }
        }
        #endregion
    }
}
