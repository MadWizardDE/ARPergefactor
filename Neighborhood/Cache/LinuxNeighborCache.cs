using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace MadWizard.ARPergefactor.Neighborhood.Cache
{
    internal class LinuxNeighborCache : ILocalIPCache
    {
        public required ILogger<LinuxNeighborCache> Logger { private get; init; }

        public required NetworkDevice Device { protected get; init; }

        void ILocalIPCache.Update(IPAddress ip, PhysicalAddress mac)
        {
            // IMPROVE add [ nud STATE ] ? which state, "permanent" or "reachable"?

            exec($"-family {ip.ToFamilyName()} neigh replace {ip} lladdr {mac.ToPlatformString()} dev {Device.Name}");
        }

        void ILocalIPCache.Delete(IPAddress ip)
        {
            exec($"-family {ip.ToFamilyName()} neigh del {ip} dev {Device.Name}");
        }

        private void exec(string arguments)
        {
            Process command = new()
            {
                StartInfo = new()
                {
                    FileName = "ip",
                    Arguments = arguments,

                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    //RedirectStandardInput = true,
                }
            };

            command.Start();
            command.WaitForExit();
            if (command.StandardError.ReadToEnd() is string message && !string.IsNullOrEmpty(message))
            {
                Logger.LogError($"Failed to execute \"ip {arguments}\" – {message.Trim()}");
            }
            else
            {
                Logger.LogTrace($"Executed \"ip {arguments}\"");
            }
        }

    }

    file static class IPAddressExt
    {
        public static string ToFamilyName(this IPAddress ip)
        {
            switch (ip.AddressFamily)
            {
                case AddressFamily.InterNetwork:
                    return "inet";
                case AddressFamily.InterNetworkV6:
                    return "inet6";

                default:
                    throw new NotSupportedException($"Unsupported address family: {ip.AddressFamily}");
            }
        }
    }

}
