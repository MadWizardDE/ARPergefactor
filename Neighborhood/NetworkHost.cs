using Autofac;
using MadWizard.ARPergefactor.Neighborhood.Tables;
using MadWizard.ARPergefactor.Reachability.Events;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace MadWizard.ARPergefactor.Neighborhood
{
    public class NetworkHost
    {
        public required ILogger<NetworkHost> Logger { protected get; init; }

        public required Network Network { get; init; }

        public string Name { get; init; }
        public string HostName { get => field ?? Name; set; } = null!;

        public PhysicalAddress? PhysicalAddress { get; set; }

        readonly IPTable table = new();
        public IEnumerable<IPAddress> IPAddresses => table;
        public IEnumerable<IPAddress> IPv4Addresses => IPAddresses.Where(ip => ip.AddressFamily == AddressFamily.InterNetwork);
        public IEnumerable<IPAddress> IPv6Addresses => IPAddresses.Where(ip => ip.AddressFamily == AddressFamily.InterNetworkV6);

        public event EventHandler<PhysicalAddressEventArgs>? PhysicalAddressChanged;

        public event EventHandler<AddressEventArgs>? AddressAdded;
        public event EventHandler<AddressEventArgs>? AddressRemoved;

        public NetworkHost(string name)
        {
            Name = name;

            table.Expired += (sender, args) =>
            {
                this.Logger?.LogTrace("Remove {Family} address '{IPAddress}' from host '{HostName}' (expired)", args.ToFamilyName(), args, Name);

                AddressRemoved?.Invoke(this, new(args));
            };
        }

        public bool AddAddress(IPAddress ip, TimeSpan? lifetime = null)
        {
            ip.RemoveScopeId();

            if (lifetime != null ? table.SetDynamicEntry(ip, lifetime.Value) : table.AddStaticEntry(ip))
            {
                Logger.LogTrace($"Add {ip.ToFamilyName()} address '{ip}' to host '{Name}'" 
                    + (lifetime != null ? $" with lifetime {lifetime}" : "")
                    + (Network.IsInLocalSubnet(ip) ? " [link-local]" : "[remote]"));

                AddressAdded?.Invoke(this, new(ip));

                return true;
            }

            return false;
        }

        public bool HasAddress(PhysicalAddress? mac = null, IPAddress? ip = null, bool both = false)
        {
            if (mac != null || ip != null)
            {
                bool hasMac = mac != null && mac.Equals(this.PhysicalAddress);
                bool hasIP = ip != null && this.IPAddresses.Contains(ip);

                return both ? hasMac && hasIP : hasMac || hasIP;
            }

            return false;
        }

        public bool RemoveAddress(IPAddress ip)
        {
            if (table.RemoveEntry(ip))
            {
                Logger.LogTrace("Remove {Family} address '{IPAddress}' from host '{HostName}'", ip.ToFamilyName(), ip, Name);

                AddressRemoved?.Invoke(this, new(ip));

                return true;
            }

            return false;
        }
    }
}