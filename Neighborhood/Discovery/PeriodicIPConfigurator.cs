using MadWizard.ARPergefactor.Config;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MadWizard.ARPergefactor.Neighborhood.Discovery
{
    internal class PeriodicIPConfigurator(ExpergefactorConfig config) : BackgroundService, IIPConfigurator
    {
        public required ILogger<PeriodicIPConfigurator> Logger { private get; init; }

        readonly List<NetworkHost> _autoIPv4 = [];
        readonly List<NetworkHost> _autoIPv6 = [];

        public void ConfigureIPv4(NetworkHost host)
        {
            _autoIPv4.Add(host);
        }

        public void ConfigureIPv6(NetworkHost host)
        {
            _autoIPv6.Add(host);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            foreach (var host in _autoIPv4)
                host.AddressFound += UpdateIPv4Address;
            foreach (var host in _autoIPv6)
                host.AddressFound += UpdateIPv6Address;

            do
            {
                foreach (var host in _autoIPv4)
                {
                    await ReplaceIPAddresses(host, AddressFamily.InterNetwork, stoppingToken);
                }

                foreach (var host in _autoIPv6)
                {
                    await ReplaceIPAddresses(host, AddressFamily.InterNetworkV6, stoppingToken);
                }
            }
            while (await ShouldRefresh(stoppingToken));

            foreach (var host in _autoIPv4)
                host.AddressFound -= UpdateIPv4Address;
            foreach (var host in _autoIPv6)
                host.AddressFound -= UpdateIPv6Address;
        }

        private void UpdateIPv4Address(object? sender, IPAddress ip)
        {
            var host = (NetworkHost)sender!;

            if (ip.AddressFamily == AddressFamily.InterNetwork)
                host.IPAddresses.Add(ip);

            Logger.LogInformation("Host '{HostName}' changed IPv4 address to '{IPAddress}'", ip, host.HostName);
        }

        private void UpdateIPv6Address(object? sender, IPAddress ip)
        {
            var host = (NetworkHost)sender!;

            if (ip.AddressFamily == AddressFamily.InterNetworkV6)
                host.IPAddresses.Add(ip);

            Logger.LogInformation("Host '{HostName}' changed IPv4 address to '{IPAddress}'", ip, host.HostName);
        }

        private async Task ReplaceIPAddresses(NetworkHost host, AddressFamily family, CancellationToken token)
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(token).WithTimeout(config.AutoTimeout);

                var result = await Dns.GetHostAddressesAsync(host.HostName, family, token);

                host.IPAddresses = new HashSet<IPAddress>([.. host.IPAddresses.Where(ip => ip.AddressFamily != family), .. result]);
            }
            catch (TimeoutException)
            {
                Logger.LogWarning("{AddressFamily}-AutoConfig failed for '{HostName}' -> TIMEOUT", family, host.HostName);
            }
            catch (SocketException ex)
            {
                if (ex.SocketErrorCode == SocketError.HostNotFound)
                {
                    Logger.LogWarning("{AddressFamily}-AutoConfig failed for '{HostName}' -> NOT_FOUND", family, host.HostName);
                }
                else
                {
                    Logger.LogError(ex, "{AddressFamily}-AutoConfig failed for '{HostName}' -> {ErrorCode}", family, host.HostName, ex.SocketErrorCode);
                }
            }
        }

        private async Task<bool> ShouldRefresh(CancellationToken stoppingToken)
        {
            if (config.AutoLatency is TimeSpan latency)
            {
                try
                {
                    await Task.Delay(latency, stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    return false;
                }

                return true;
            }

            return false;
        }
    }

    file static class CancellationTokenHelper
    {
        internal static CancellationTokenSource WithTimeout(this CancellationTokenSource source, TimeSpan timeout)
        {
            var cts = CancellationTokenSource.CreateLinkedTokenSource(source.Token);
            cts.CancelAfter(timeout);
            return cts;
        }
    }
}
