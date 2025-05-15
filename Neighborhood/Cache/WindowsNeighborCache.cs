using MadWizard.ARPergefactor.Config;
using MadWizard.ARPergefactor.Impersonate.ARP;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.ARPergefactor.Neighborhood.Cache
{
    internal class WindowsNeighborCache(ExpergefactorConfig config) : ILocalARPCache
    {
        public required ILogger<WindowsNeighborCache> Logger { private get; init; }

        public required NetworkDevice Device { protected get; init; }

        private string InterfaceName => "Bridged Ethernet";

        void ILocalARPCache.Update(PhysicalAddress mac, IPAddress ip)
        {
            netsh($"interface ip delete neighbors \"{InterfaceName}\" {ip}");
            netsh($"interface ip add neighbors \"{InterfaceName}\" {ip} {mac.ToPlatformString()}");
        }

        void ILocalARPCache.Delete(IPAddress ip)
        {
            netsh($"interface ip delete neighbors \"{InterfaceName}\" {ip}");
        }

        private void netsh(string arguments)
        {
            if (config.Simulate)
            {
                Logger.LogDebug($"Simulated \"netsh {arguments}\""); return;
            }

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
                Logger.LogDebug($"Executed \"netsh {arguments}\"");
            }
        }

    }
}
