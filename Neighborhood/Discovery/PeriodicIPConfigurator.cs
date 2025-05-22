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
    internal class PeriodicIPConfigurator(AutoMethod method) : BackgroundService, IIPConfigurator
    {
        public required ILogger<PeriodicIPConfigurator> Logger { private get; init; }

        readonly List<NetworkHost> _autoIPv4 = [];
        readonly List<NetworkHost> _autoIPv6 = [];

        public void ConfigureIPv4(NetworkHost host)
        {
            UpdateIPAddresses(host, AddressFamily.InterNetwork, CancellationToken.None).Wait();

            _autoIPv4.Add(host);
        }

        public void ConfigureIPv6(NetworkHost host)
        {
            UpdateIPAddresses(host, AddressFamily.InterNetworkV6, CancellationToken.None).Wait();

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
                    await UpdateIPAddresses(host, AddressFamily.InterNetwork, stoppingToken);
                }

                foreach (var host in _autoIPv6)
                {
                    await UpdateIPAddresses(host, AddressFamily.InterNetworkV6, stoppingToken);
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

        private async Task UpdateIPAddresses(NetworkHost host, AddressFamily family, CancellationToken token)
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
                            Logger.LogWarning("AutoConfig[{AddressFamily}] failed for '{HostName}' -> NOT_FOUND", family, host.HostName);
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

                foreach (var ip in host.IPAddresses.Where(ip => ip.AddressFamily == family && !addresses.Contains(ip)))
                    host.RemoveAddress(ip);
                foreach (var ip in addresses.Where(ip => !host.IPAddresses.Contains(ip)))
                    host.AddAddress(ip);

                if (host.Name == "Bitfroest")
                    host.AddAddress(IPAddress.Parse("fe80::b2f2:8ff:fe0a:d114"));
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
