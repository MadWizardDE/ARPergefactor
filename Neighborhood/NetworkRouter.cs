using System.Net;

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
