using Autofac;
using Autofac.Core.Lifetime;
using MadWizard.ARPergefactor.Config;
using MadWizard.ARPergefactor.Impersonate;
using MadWizard.ARPergefactor.Neighborhood;
using MadWizard.ARPergefactor.Wake;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PacketDotNet;
using PacketDotNet.Utils;
using SharpPcap;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Channels;

namespace MadWizard.ARPergefactor
{
    /// <summary>
    /// This is the main entrypoint into the application.
    /// It is responsible for starting to monitor the configured network interfaces.
    /// 
    /// https://en.wikipedia.org/wiki/Knocker-up
    /// </summary>
    internal class KnockerUp : IHostedService
    {
        public required ILogger<KnockerUp> Logger { private get; init; }

        public required IEnumerable<Network> Networks { private get; init; }

        async Task IHostedService.StartAsync(CancellationToken cancellationToken)
        {
            foreach (var network in Networks)
            {
                network.StartMonitoring();
            }
        }

        async Task IHostedService.StopAsync(CancellationToken cancellationToken)
        {
            foreach (var network in Networks)
            {
                network.StopMonitoring();
            }
        }
    }
}
