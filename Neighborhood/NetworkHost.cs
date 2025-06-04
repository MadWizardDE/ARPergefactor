using Autofac;
using Autofac.Core;
using Autofac.Core.Lifetime;
using MadWizard.ARPergefactor.Config;
using MadWizard.ARPergefactor.Impersonate;
using MadWizard.ARPergefactor.Neighborhood.Filter;
using MadWizard.ARPergefactor.Neighborhood.Tables;
using MadWizard.ARPergefactor.Reachability.Events;
using MadWizard.ARPergefactor.Wake;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PacketDotNet;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

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

        public event EventHandler<AddressEventArgs>? AddressAdded;
        public event EventHandler<AddressEventArgs>? AddressRemoved;

        public NetworkHost(string name)
        {
            Name = name;

            table.Expired += (sender, args) =>
            {
                this.Logger?.LogDebug("Remove {Family} address '{IPAddress}' from Host '{HostName}' (expired)", args.ToFamilyName(), args, Name);

                AddressRemoved?.Invoke(this, new(args));
            };
        }

        public bool AddAddress(IPAddress ip, TimeSpan? lifetime = null)
        {
            if (lifetime != null ? table.SetDynamicEntry(ip, lifetime.Value) : table.AddStaticEntry(ip))
            {
                Logger.LogDebug($"Add {ip.ToFamilyName()} address '{ip}' to Host '{Name}'" 
                    + (lifetime != null ? $" with lifetime {lifetime}" : ""));

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
                Logger.LogDebug("Remove {Family} address '{IPAddress}' from Host '{HostName}'", ip.ToFamilyName(), ip, Name);

                AddressRemoved?.Invoke(this, new(ip));

                return true;
            }

            return false;
        }
    }
}