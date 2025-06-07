using MadWizard.ARPergefactor.Config;
using MadWizard.ARPergefactor.Neighborhood.Methods;
using MadWizard.ARPergefactor.Reachability;
using MadWizard.ARPergefactor.Reachability.Events;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Timer = System.Timers.Timer;

namespace MadWizard.ARPergefactor.Neighborhood.Discovery
{
    internal class PeriodicIPConfigurator(AutoDetectMethod method) : IIPConfigurator, IDisposable
    {
        public required ILogger<PeriodicIPConfigurator> Logger { private get; init; }

        public required ReachabilityService Reachability { private get; init; }

        readonly Dictionary<NetworkHost, HashSet<IPAddress>> _autoIPv4 = [];
        readonly Dictionary<NetworkHost, HashSet<IPAddress>> _autoIPv6 = [];

        readonly CancellationTokenSource _autoCancellation = new();

        Timer? _autoTimer;

        public void MaybeStartTimer()
        {
            if (method.Latency is TimeSpan latency && _autoTimer == null)
            {
                _autoTimer = new Timer(latency.TotalMilliseconds);
                _autoTimer.Elapsed += RefreshAddresses;
                _autoTimer.AutoReset = false;
                _autoTimer.Start();
            }
        }

        public void ConfigureIPv4(NetworkHost host)
        {
            Reachability.HostAddressAdvertisement += UpdateIPv4Address;

            HashSet<IPAddress> auto = [];

            RefreshIPAddresses(host, auto, AddressFamily.InterNetwork, CancellationToken.None).Wait();

            _autoIPv4[host] = auto;

            MaybeStartTimer();
        }

        private void UpdateIPv4Address(object? sender, HostAddressAdvertisement args)
        {
            if (args.IPAddress.AddressFamily == AddressFamily.InterNetwork)
            {
                if (args.Host.AddAddress(args.IPAddress, args.Lifetime))
                {
                    Logger.LogDebug("Host '{HostName}' advertised unknown {Family} address '{IPAddress}'", 
                        args.Host.Name, args.IPAddress.ToFamilyName(), args.IPAddress);

                    if (args.Lifetime is null)
                    {
                        _autoIPv4[args.Host].Add(args.IPAddress);
                    }
                }
            }
        }

        public void ConfigureIPv6(NetworkHost host)
        {
            Reachability.HostAddressAdvertisement += UpdateIPv6Address;

            HashSet<IPAddress> auto = [];

            RefreshIPAddresses(host, auto, AddressFamily.InterNetworkV6, CancellationToken.None).Wait();

            _autoIPv6[host] = auto;

            MaybeStartTimer();
        }

        private void UpdateIPv6Address(object? sender, HostAddressAdvertisement args)
        {
            if (args.IPAddress.AddressFamily == AddressFamily.InterNetworkV6)
            {
                if (args.Host.AddAddress(args.IPAddress, args.Lifetime))
                {
                    Logger.LogDebug("Host '{HostName}' advertised unknown {Family} address '{IPAddress}'",
                        args.Host.Name, args.IPAddress.ToFamilyName(), args.IPAddress);

                    if (args.Lifetime is null)
                    {
                        _autoIPv6[args.Host].Add(args.IPAddress);
                    }
                }
            }
        }

        private async void RefreshAddresses(object? sender, ElapsedEventArgs args)
        {
            _autoTimer?.Stop();

            try
            {
                Logger.LogTrace("Refreshing auto-configured IP addresses...");

                foreach (var host in _autoIPv4)
                {
                    await RefreshIPAddresses(host.Key, host.Value, AddressFamily.InterNetwork, _autoCancellation.Token);
                }

                foreach (var host in _autoIPv6)
                {
                    await RefreshIPAddresses(host.Key, host.Value, AddressFamily.InterNetworkV6, _autoCancellation.Token);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error while refreshing auto-configured IP addresses: {Message}", ex.Message);
            }
            finally
            {
                if (!_autoCancellation.IsCancellationRequested)
                {
                    _autoTimer?.Start();
                }
            }
        }

        private async Task RefreshIPAddresses(NetworkHost host, HashSet<IPAddress> auto, AddressFamily family, CancellationToken token)
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
                        case SocketError.TryAgain:
                            Logger.LogTrace("AutoConfig[{AddressFamily}] failed for '{HostName}' -> TRY_AGAIN", family, host.HostName);
                            break;

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

        void IDisposable.Dispose()
        {
            _autoCancellation?.Cancel();
            _autoTimer?.Stop();
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
