using MadWizard.ARPergefactor.Config;
using MadWizard.ARPergefactor.Neighborhood.Methods;
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
    internal class PeriodicIPConfigurator(AutoDetectMethod method) : BackgroundService, IIPConfigurator
    {
        public required ILogger<PeriodicIPConfigurator> Logger { private get; init; }

        readonly Dictionary<NetworkHost, HashSet<IPAddress>> _autoIPv4 = [];
        readonly Dictionary<NetworkHost, HashSet<IPAddress>> _autoIPv6 = [];

        public void ConfigureIPv4(NetworkHost host)
        {
            host.AddressAdvertised += UpdateIPv4Address;

            HashSet<IPAddress> auto = [];

            UpdateIPAddresses(host, auto, AddressFamily.InterNetwork, CancellationToken.None).Wait();

            _autoIPv4[host] = auto;
        }

        public void ConfigureIPv6(NetworkHost host)
        {
            host.AddressAdvertised += UpdateIPv6Address;

            HashSet<IPAddress> auto = [];

            UpdateIPAddresses(host, auto, AddressFamily.InterNetworkV6, CancellationToken.None).Wait();

            _autoIPv6[host] = auto;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            //foreach (var host in _autoIPv4.Keys)
            //foreach (var host in _autoIPv6.Keys)

            while (await ShouldRefresh(stoppingToken))
            {
                Logger.LogTrace("Refreshing auto-configured IP addresses...");

                foreach (var host in _autoIPv4)
                {
                    await UpdateIPAddresses(host.Key, host.Value, AddressFamily.InterNetwork, stoppingToken);
                }

                foreach (var host in _autoIPv6)
                {
                    await UpdateIPAddresses(host.Key, host.Value, AddressFamily.InterNetworkV6, stoppingToken);
                }
            }

            //foreach (var host in _autoIPv4.Keys)
            //    host.AddressAdvertised -= UpdateIPv4Address;
            //foreach (var host in _autoIPv6.Keys)
            //    host.AddressAdvertised -= UpdateIPv6Address;
        }

        private void UpdateIPv4Address(object? sender, IPAdvertisementEventArgs args)
        {
            var host = (NetworkHost)sender!;
            if (args.IP.AddressFamily == AddressFamily.InterNetwork)
                host.AddAddress(args.IP, args.Lifetime);
        }

        private void UpdateIPv6Address(object? sender, IPAdvertisementEventArgs args)
        {
            var host = (NetworkHost)sender!;
            if (args.IP.AddressFamily == AddressFamily.InterNetworkV6)
                host.AddAddress(args.IP, args.Lifetime);
        }

        private async Task UpdateIPAddresses(NetworkHost host, HashSet<IPAddress> auto, AddressFamily family, CancellationToken token)
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(token).WithTimeout(method.Timeout);

                IPAddress[] addresses = []; // in any case, clear the IP addresses on the host
                try
                {
                    addresses = await Dns.GetHostAddressesAsync(host.HostName, family, token);
                }
                catch (SocketException ex)
                {
                    switch (ex.SocketErrorCode)
                    {
                        case SocketError.HostNotFound:
                            Logger.LogDebug("AutoConfig[{AddressFamily}] failed for '{HostName}' -> NOT_FOUND", family, host.HostName);
                            break;

                        case SocketError.NoData:
                            // that simply means, there are not IP addresses known to the DNS
                            Logger.LogTrace("AutoConfig[{AddressFamily}] failed for '{HostName}' -> NO_DATA", family, host.HostName);
                            break;

                        default:
                            Logger.LogError(ex, "AutoConfig[{AddressFamily}] failed for '{HostName}' -> {ErrorCode}", family, host.HostName, ex.SocketErrorCode);
                            break;
                    }
                }

                foreach (var ip in auto.Except(addresses))
                    host.RemoveAddress(ip);
                foreach (var ip in addresses)
                    host.AddAddress(ip);

                auto.Clear();
                auto.UnionWith(addresses);
            }
            catch (TimeoutException)
            {
                Logger.LogWarning("AutoConfig[{AddressFamily}] failed for '{HostName}' -> TIMEOUT", family, host.HostName);
            }
        }

        private async Task<bool> ShouldRefresh(CancellationToken stoppingToken)
        {
            if (method.Latency is TimeSpan latency)
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
