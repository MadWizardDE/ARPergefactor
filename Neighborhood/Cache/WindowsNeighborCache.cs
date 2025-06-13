using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace MadWizard.ARPergefactor.Neighborhood.Cache
{
    internal class WindowsNeighborCache : ILocalIPCache
    {
        public required ILogger<WindowsNeighborCache> Logger { private get; init; }

        public required NetworkDevice Device { protected get; init; }

        void ILocalIPCache.Update(IPAddress ip, PhysicalAddress mac)
        {
            netsh($"interface {ip.ToContextName()} delete neighbors \"{Device.Name}\" {ip}");
            netsh($"interface {ip.ToContextName()} add neighbors \"{Device.Name}\" {ip} {mac.ToPlatformString()}");
        }

        void ILocalIPCache.Delete(IPAddress ip)
        {
            netsh($"interface {ip.ToContextName()} delete neighbors \"{Device.Name}\" {ip}");
        }

        private void netsh(string arguments)
        {
            Process command = new()
            {
                StartInfo = new()
                {
                    FileName = "netsh",
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
                Logger.LogError($"Failed to execute \"netsh {arguments}\" – {message.Trim()}");
            }
            else
            {
                Logger.LogTrace($"Executed \"netsh {arguments}\"");
            }
        }

    }

    file static class IPAddressExt
    {
        public static string ToContextName(this IPAddress ip)
        {
            switch (ip.AddressFamily)
            {
                case AddressFamily.InterNetwork:
                    return "ipv4";
                case AddressFamily.InterNetworkV6:
                    return "ipv6";

                default:
                    throw new NotSupportedException($"Unsupported address family: {ip.AddressFamily}");
            }
        }
    }
}