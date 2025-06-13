using Autofac;
using Autofac.Core;

using MadWizard.ARPergefactor.Config;
using MadWizard.ARPergefactor.Impersonate;
using MadWizard.ARPergefactor.Neighborhood.Filter;
using MadWizard.ARPergefactor.Neighborhood.Methods;
using MadWizard.ARPergefactor.Reachability.Events;
using MadWizard.ARPergefactor.Wake.Filter.Rules;
using Microsoft.Extensions.Options;
using System.Net;

namespace MadWizard.ARPergefactor.Neighborhood.Discovery
{
    internal class StaticNetworkDiscovery : IIEnumerable<Network> //, IDisposable TODO
    {
        private readonly List<Network> _networks = [];

        private StaticTrafficShapeCollector _shapes = new();

        private int _dynamicRouterCount = 0;

        public StaticNetworkDiscovery(ILifetimeScope root, IOptions<ExpergefactorConfig> config)
        {
            foreach (var networkConfig in config.Value.Network ?? [])
            {
                var options = new NetworkOptions()
                {
                    WatchScope = config.Value.Scope,
                    WatchUDPPort = networkConfig.WatchUDPPort,
                };

                _shapes += new ARPTrafficShape();
                _shapes += new NDPTrafficShape();
                _shapes += new WOLTrafficShape();

                if (options.WatchUDPPort is uint port)
                    _shapes += new UDPTrafficShape(port);

                var network = RegisterNetwork(root, networkConfig, options);

                _networks.Add(network);
            }
        }

        private Network RegisterNetwork(ILifetimeScope root, NetworkConfig config, NetworkOptions options)
        {
            var scopeNetwork = root.BeginLifetimeScope(MatchingScopeLifetimeTags.NetworkLifetimeScopeTag, builder =>
            {
                builder.RegisterType<Network>()
                       .WithParameter(TypedParameter.From(options))
                       .PropertiesAutowired(PropertyWiringOptions.AllowCircularDependencies)
                       .SingleInstance()
                       .AsSelf();

                builder.RegisterType<NetworkDevice>()
                       .WithParameter(TypedParameter.From(config.Interface))
                       .PropertiesAutowired(PropertyWiringOptions.AllowCircularDependencies)
                       .AsImplementedInterfaces()
                       .SingleInstance()
                       .AsSelf();

                builder.RegisterType<PeriodicIPConfigurator>()
                       .WithParameter(TypedParameter.From(config.AutoMethod))
                       .AsImplementedInterfaces()
                       .InstancePerNetwork();

                RegisterRequestFilters(builder, config);
            });

            var network = scopeNetwork.Resolve<Network>();

            _shapes.PushTo(scopeNetwork);

            foreach (var configHost in config.Host ?? [])
                network.AddHost(RegisterHost(scopeNetwork, config, configHost));

            foreach (var configRouter in config.Router ?? [])
            {
                foreach (var configVPNClient in configRouter.VPNClient ?? [])
                {
                    network.AddHost(RegisterHost(scopeNetwork, config, configVPNClient));
                }

                network.AddHost(RegisterHost(scopeNetwork, config, configRouter));
            }

            if (config.AutoDetect.HasFlag(AutoDetectType.Router))
            {
                DetectStandardGateway(scopeNetwork, config);
            }

            foreach (var configHost in config.WatchHost ?? [])
            {
                network.AddHost(RegisterHost(scopeNetwork, config, configHost));

                foreach (var configHostVirtual in configHost.VirtualHost ?? [])
                {
                    network.AddHost(RegisterVirtualHost(scopeNetwork, config, configHost, configHostVirtual));
                }
            }

            //if (config.AutoDetect.HasFlag(AutoDetectType.Router) && config.AutoDetect.HasFlag(AutoDetectType.IPv6))
            //{
            //    async void HandleRouterAdvertisement(object? sender, RouterAdvertisementEventArgs args)
            //    {
            //        network.AddHost(await RegisterDynamicRouter(scopeNetwork, config, args));
            //    }

            //    network.RouterAdvertised += HandleRouterAdvertisement;
            //}

            root.Disposer.AddInstanceForDisposal(scopeNetwork);

            return network;
        }

        private void DetectStandardGateway(ILifetimeScope scopeNetwork, NetworkConfig configNetwork)
        {
            var network = scopeNetwork.Resolve<Network>();
            var networkDevice = scopeNetwork.Resolve<NetworkDevice>();

            var configGateway = new StandardGatewayInfo(networkDevice.Interface, configNetwork.AutoDetect)
            {
                Name = "StandardGateway",

                AutoDetect = AutoDetectType.None, // add no dynamic IPs, because we have no hostname
            };

            if (network.FindHostByIP(configGateway.IPAddresses) is NetworkRouter router)
            {
                foreach (var additionalIPs in configGateway.IPAddresses)
                    router.AddAddress(additionalIPs);
            }
            else
            {
                network.AddHost(RegisterHost(scopeNetwork, configNetwork, configGateway));
            }
        }

        private async Task<NetworkRouter> RegisterDynamicRouter(ILifetimeScope scopeNetwork, NetworkConfig configNetwork, RouterAdvertisement advert)
        {
            string name;

            try
            {
                var entry = await Dns.GetHostEntryAsync(advert.IPAddress);

                name = entry.HostName;
            }
            catch
            {
                name = $"DynamicRouter{++_dynamicRouterCount}";
            }

            var scopeHost = scopeNetwork.BeginLifetimeScope(MatchingScopeLifetimeTags.NetworkHostLifetimeScopeTag, builder =>
            {
                var register = builder.RegisterType<NetworkRouter>().As<NetworkHost>()
                    .WithParameter(new TypedParameter(typeof(string), name))
                    .WithParameter(new TypedParameter(typeof(NetworkRouterOptions), new NetworkRouterOptions()))
                    .WithParameter(new TypedParameter(typeof(IEnumerable<NetworkHost>), new List<NetworkHost>()))
                    .SingleInstance()
                    .AsSelf();
            });

            var router = scopeHost.Resolve<NetworkRouter>();

            router.PhysicalAddress = advert.PhysicalAddress;
            router.AddAddress(advert.IPAddress, advert.Lifetime);

            scopeNetwork.Disposer.AddInstanceForDisposal(scopeHost);

            return router;
        }

        private NetworkHost RegisterHost(ILifetimeScope scopeNetwork, NetworkConfig configNetwork, HostInfo config)
        {
            var scopeHost = scopeNetwork.BeginLifetimeScope(MatchingScopeLifetimeTags.NetworkHostLifetimeScopeTag, builder =>
            {
                var register = config switch
                {
                    PhysicalHostInfo configPhysical =>
                        builder.RegisterType<NetworkWatchHost>().As<NetworkHost>()
                            .WithProperty(TypedParameter.From(configPhysical.MakePingMethod(configNetwork)))
                            .WithProperty(TypedParameter.From(configPhysical.MakePoseMethod(configNetwork)))
                            .WithProperty(TypedParameter.From(configPhysical.MakeWakeMethod(configNetwork)))
                            .SingleInstance()
                            .AsSelf(),

                    RouterInfo configRouter =>
                        builder.RegisterType<NetworkRouter>().As<NetworkHost>()
                            .WithParameter(TypedParameter.From(configRouter.Options))
                            .WithParameter(NetworkHostsParameter.FindBy([.. configRouter.VPNClient.Select(h => h.Name)]))
                            .SingleInstance()
                            .AsSelf(),

                    HostInfo configHost when config.GetType() == typeof(HostInfo) =>
                        builder.RegisterType<NetworkHost>().As<NetworkHost>()
                            .SingleInstance()
                            .AsSelf(),

                    _ => throw new NotSupportedException($"Host type '{config.GetType()}' is not supported."),
                };

                register.WithParameter(new TypedParameter(typeof(string), config.Name));

                RegisterRequestFilters(builder, config);
                if (config is PhysicalHostInfo wake)
                    RegisterServices(builder, wake);
            });

            scopeNetwork.Disposer.AddInstanceForDisposal(scopeHost);

            return ConfigureHost(scopeHost, configNetwork, config);
        }

        private NetworkHost RegisterVirtualHost(ILifetimeScope scopeNetwork, NetworkConfig configNetwork, PhysicalHostInfo configPhysical, VirtualHostInfo config)
        {
            var scopeHost = scopeNetwork.BeginLifetimeScope(MatchingScopeLifetimeTags.NetworkHostLifetimeScopeTag, builder =>
            {
                builder.RegisterType<VirtualWatchHost>().As<NetworkWatchHost>().As<NetworkHost>()
                    .WithParameter(new TypedParameter(typeof(string), config.Name))
                    .WithParameter(HostParameter<NetworkWatchHost>.FindBy(configPhysical.Name))
                    .WithProperty(TypedParameter.From(config.MakePingMethod(configNetwork)))
                    .WithProperty(TypedParameter.From(config.MakePoseMethod(configNetwork)))
                    .WithProperty(TypedParameter.From(config.MakeWakeMethod(configNetwork)))
                    .WithProperty(TypedParameter.From(config.WakeRedirect))
                    .SingleInstance()
                    .AsSelf();

                RegisterRequestFilters(builder, config);
                RegisterServices(builder, config);
            });

            scopeNetwork.Disposer.AddInstanceForDisposal(scopeHost);

            return ConfigureHost(scopeHost, configNetwork, config);
        }

        private NetworkHost ConfigureHost(ILifetimeScope scopeHost, NetworkConfig configNetwork, HostInfo config)
        {
            var host = scopeHost.Resolve<NetworkHost>();

            if (config.HostName != null)
                host.HostName = config.HostName;

            host.PhysicalAddress = config.PhysicalAddress;
            foreach (var ip in config.IPAddresses)
                host.AddAddress(ip);

            var autoDetect = config.AutoDetect ?? configNetwork.AutoDetect;
            if (scopeHost.ResolveOptional<IIPConfigurator>() is IIPConfigurator ipConfigurator)
            {
                if (autoDetect.HasFlag(AutoDetectType.IPv4))
                    ipConfigurator.ConfigureIPv4(host);
                if (autoDetect.HasFlag(AutoDetectType.IPv6))
                    ipConfigurator.ConfigureIPv6(host);
            }

            if (autoDetect.HasFlag(AutoDetectType.IPv4) || host.IPv4Addresses.Any())
                _shapes += new IPv4TrafficShape();
            if (autoDetect.HasFlag(AutoDetectType.IPv6) || host.IPv6Addresses.Any())
                _shapes += new IPv6TrafficShape();

            if (host is NetworkWatchHost watch && watch.PoseMethod.Latency is TimeSpan latency)
            {
                var imposter = scopeHost.Resolve<Imposter>();

                imposter.ConfigurePreemptive(latency);
            }

            _shapes.PushTo(scopeHost);

            return host;
        }

        private void RegisterServices(ContainerBuilder builder, WakeHostInfo host)
        {
            foreach (var service in host.Service ?? [])
            {
                // TODO implement NetworkServices

                RegisterServiceFilter(builder, host, service);
            }
        }

        private void RegisterRequestFilters(ContainerBuilder builder, FilterRuleContainer scope)
        {
            builder.RegisterType<StaticHostFilterRule>().AsSelf();
            builder.RegisterType<DynamicHostFilterRule>().AsSelf();

            foreach (var filter in scope.HostFilterRule ?? [])
            {
                if (filter.IsCompound)
                {
                    RegisterServiceFilters(builder, filter);
                    RegisterPingFilter(builder, filter);
                }
                else
                {
                    if (filter.IsDynamic)
                    {
                        builder.RegisterType<DynamicHostFilterRule>()
                            .WithParameter(TypedParameter.From(filter.Type))
                            .WithParameter(HostParameter<NetworkHost>.FindBy(filter.Name))
                            .As<FilterRule>().As<HostFilterRule>()
                            .SingleInstance();
                    }
                    else
                    {
                        builder.RegisterType<StaticHostFilterRule>()
                            .WithParameter(TypedParameter.From(filter.Type))
                            .WithProperty(TypedParameter.From(filter.PhysicalAddress))
                            .WithProperty(TypedParameter.From(filter.IPAddresses.ToList()))
                            .As<FilterRule>().As<HostFilterRule>()
                            .SingleInstance();
                    }
                }
            }

            RegisterServiceFilters(builder, scope);
            RegisterPingFilter(builder, scope);
        }

        private void RegisterServiceFilters(ContainerBuilder builder, FilterRuleContainer scope)
        {
            // iterate over all service filter types
            IEnumerable<ServiceFilterRuleInfo> serviceFilters = (scope.ServiceFilterRule ?? [])
                .Concat(scope.HTTPFilterRule != null ? [scope.HTTPFilterRule] : []);

            foreach (var filter in serviceFilters)
            {
                RegisterServiceFilter(builder, scope, filter);
            }
        }

        private void RegisterServiceFilter(ContainerBuilder builder, FilterRuleContainer scope, ServiceFilterRuleInfo filter)
        {
            var service = new TransportService(filter.Name, filter.Protocol, filter.Port);

            if (service.ProtocolType.HasFlag(TransportPortocolType.TCP))
                _shapes += new TCPTrafficShape(service.Port);
            if (service.ProtocolType.HasFlag(TransportPortocolType.UDP))
                _shapes += new UDPTrafficShape(service.Port);

            var register = builder.RegisterType<ServiceFilterRule>()
                .WithParameter(TypedParameter.From(filter.Type))
                .WithParameter(TypedParameter.From(service))
                .SingleInstance()
                .As<FilterRule>()
                .AsSelf();

            if (scope is HostFilterRuleInfo filterHost)
            {
                register.WithProperty(HostFilterRuleParameter.From(filterHost));
            }

            List<PayloadFilterRule> payloadFilters = [];

            if (filter is HTTPFilterRuleInfo http)
            {
                foreach (var request in http.RequestFilterRule ?? [])
                {
                    throw new NotImplementedException("RequestFilterRule is not implemented, yet.");

                    //var payload = new HTTPRequestFilterRule
                    //{
                    //    Type = request.Type,
                    //    Method = request.Method,
                    //    Path = request.Path,
                    //    Version = request.Version,
                    //    Host = request.Host,
                    //};

                    //foreach (var header in request.Header ?? [])
                    //    payload.Header[header.Name] = !string.IsNullOrWhiteSpace(header.Text) ? header.Text : null;
                    //foreach (var cookie in request.Cookie ?? [])
                    //    payload.Cookie[cookie.Name] = !string.IsNullOrWhiteSpace(cookie.Value) ? cookie.Value : null;

                    //payloadFilters.Add(payload);
                }
            }

            register.WithProperty(TypedParameter.From(payloadFilters));
        }

        private void RegisterPingFilter(ContainerBuilder builder, FilterRuleContainer scope)
        {
            if (scope.PingFilterRule is PingFilterRuleInfo filter)
            {
                _shapes += new ICMPEchoTrafficShape();

                var register = builder.RegisterType<PingFilterRule>()
                    .WithParameter(TypedParameter.From(filter.Type))
                    .SingleInstance()
                    .As<FilterRule>()
                    .AsSelf();

                if (scope is HostFilterRuleInfo filterHost)
                {
                    register.WithProperty(HostFilterRuleParameter.From(filterHost));
                }
            }
        }

        IEnumerator<Network> IEnumerable<Network>.GetEnumerator()
        {
            return _networks.GetEnumerator();
        }
    }

    file class HostParameter<T>(string name) : ResolvedParameter(
        (pi, ctx) => pi.ParameterType == typeof(T),
        (pi, ctx) => ResolveWith(ctx, name)) where T : NetworkHost
    {
        private static NetworkHost ResolveWith(IComponentContext ctx, string name)
        {
            if (ctx.Resolve<Network>().Hosts[name] is T host)
                return host;

            throw new KeyNotFoundException(name);
        }

        internal static HostParameter<T> FindBy(string name) => new(name);
    }

    file class NetworkHostsParameter(params string[] names) : ResolvedParameter(
        (pi, ctx) => pi.ParameterType == typeof(IEnumerable<NetworkHost>),
        (pi, ctx) => ResolveWith(ctx, names))
    {
        private static IEnumerable<NetworkHost> ResolveWith(IComponentContext ctx, IEnumerable<string> names)
        {
            List<NetworkHost> hosts = [];

            foreach (var name in names)
                if (ctx.Resolve<Network>().Hosts[name] is NetworkHost host)
                    hosts.Add(host);
                else
                    throw new KeyNotFoundException(name);

            return hosts;
        }

        internal static NetworkHostsParameter FindBy(params string[] names) => new(names);
    }

    file class HostFilterRuleParameter(HostFilterRuleInfo host) : ResolvedParameter(
        (pi, ctx) => pi.ParameterType == typeof(HostFilterRule), (pi, ctx) =>
        {
            if (host.IsDynamic)
            {
                return ctx.Resolve<DynamicHostFilterRule>(TypedParameter.From(host.Type),
                    HostParameter<NetworkHost>.FindBy(host.Name));
            }
            else
            {
                return ctx.Resolve<StaticHostFilterRule>(TypedParameter.From(host.Type),
                    TypedParameter.From(host.PhysicalAddress),
                    TypedParameter.From(host.IPAddresses.ToList()));
            }
        })
    {
        internal static HostFilterRuleParameter From(HostFilterRuleInfo host) => new(host);
    }
}
