using MadWizard.ARPergefactor.Config;
using PacketDotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Net.NetworkInformation;
using MadWizard.ARPergefactor.Reachability;

namespace MadWizard.ARPergefactor.Neighborhood
{
    internal class NetworkRouter(string name) : NetworkHost(name)
    {
        public required NetworkRouterOptions Options { get; init; }

        public required IEnumerable<NetworkHost> VPNClients { get; init; }

        public DateTime? LastVPN { get; internal set; }

        public bool AllowWakeByVPNClients => VPNClients.Any();

        public NetworkHost? FindVPNClient(IPAddress? ip)
        {
            foreach (var host in VPNClients)
                if (host.HasAddress(ip:ip))
                    return host;

            return null;
        }
    }
}
