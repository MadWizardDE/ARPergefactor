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
            ReplaceIPAddresses(host, AddressFamily.InterNetwork, CancellationToken.None).Wait();

            _autoIPv4.Add(host);
        }

        public void ConfigureIPv6(NetworkHost host)
        {
            ReplaceIPAddresses(host, AddressFamily.InterNetworkV6, CancellationToken.None).Wait();

            _autoIPv6.Add(host);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            foreach (var host in _autoIPv4)
                host.AddressFound += UpdateIPv4Address;
            foreach (var host in _autoIPv6)
                host.AddressFound += UpdateIPv6Address;

            while (await ShouldRefresh(stoppingToken))
            {
                Logger.LogTrace("Refreshing auto-configured IP addresses...");

                foreach (var host in _autoIPv4)
                {
                    await ReplaceIPAddresses(host, AddressFamily.InterNetwork, stoppingToken);
                }

                foreach (var host in _autoIPv6)
                {
                    await ReplaceIPAddresses(host, AddressFamily.InterNetworkV6, stoppingToken);
                }
            }

            foreach (var host in _autoIPv4)
                host.AddressFound -= UpdateIPv4Address;
            foreach (var host in _autoIPv6)
                host.AddressFound -= UpdateIPv6Address;
        }

        private void UpdateIPv4Address(object? sender, IPEventArgs args)
        {
            var host = (NetworkHost)sender!;
            if (args.IP.AddressFamily == AddressFamily.InterNetwork)
                host.AddAddress(args.IP);
        }

        private void UpdateIPv6Address(object? sender, IPEventArgs args)
        {
            var host = (NetworkHost)sender!;
            if (args.IP.AddressFamily == AddressFamily.InterNetworkV6)
                host.AddAddress(args.IP);
        }

        private async Task ReplaceIPAddresses(NetworkHost host, AddressFamily family, CancellationToken token)
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(token).WithTimeout(config.AutoTimeout);

                var result = await Dns.GetHostAddressesAsync(host.HostName, family, token);

                foreach (var ip in host.IPAddresses.Where(ip => ip.AddressFamily == family && !result.Contains(ip)))
                    host.RemoveAddress(ip);
                foreach (var ip in result.Where(ip => !host.IPAddresses.Contains(ip)))
                    host.AddAddress(ip);
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
