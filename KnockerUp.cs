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
    internal class KnockerUp : BackgroundService
    {
        public required ILogger<KnockerUp> Logger { private get; init; }

        public required IEnumerable<Network> Networks { private get; init; }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!Networks.Any())
            {
                throw new Exception("No networks configured for monitoring. Please configure at least one network interface.");
            }

            Logger.LogDebug("Start monitoring networks...");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    foreach (var network in Networks)
                    {
                        network.RefreshDevice();

                        if (network.IsAvailable && !network.IsMonitoring)
                        {
                            network.StartMonitoring();
                        }
                        else if (!network.IsAvailable && network.IsMonitoring)
                        {
                            network.StopMonitoring();
                        }
                    }

                    await Task.Delay(1000, stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }

            foreach (var network in Networks)
            {
                network.StopMonitoring();
            }

            Logger.LogDebug("Stopped monitoring networks.");
        }
    }
}
