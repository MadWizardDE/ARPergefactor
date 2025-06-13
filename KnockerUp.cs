using MadWizard.ARPergefactor.Neighborhood;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

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
