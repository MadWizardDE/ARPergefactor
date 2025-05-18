using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MadWizard.ARPergefactor.Config;
using MadWizard.ARPergefactor.Neighborhood;

namespace MadWizard.ARPergefactor.Neighborhood.Cache
{
    internal class LocalARPCache(ExpergefactorConfig config)
    {
        public required ILogger<LocalARPCache> Logger { private get; init; }

        public void Update(PhysicalAddress mac, IPAddress ip) => arp($"-s {ip} {mac.ToPlatformString()}");
        public void Delete(IPAddress ip) => arp($"-d {ip}");

        private void arp(string arguments)
        {
            Process command = new()
            {
                StartInfo = new()
                {
                    FileName = "arp",
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
                Logger.LogError($"Failed to execute \"arp {arguments}\" – {message.Trim()}");
            }
            else
            {
                Logger.LogTrace($"Executed \"arp {arguments}\"");
            }
        }
    }
}
