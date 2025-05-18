using Autofac;
using Autofac.Core;

using MadWizard.ARPergefactor.Config;
using MadWizard.ARPergefactor.Impersonate;
using MadWizard.ARPergefactor.Neighborhood.Filter;
using MadWizard.ARPergefactor.Request.Filter.Rules;
using MadWizard.ARPergefactor.Request.Filter.Rules.Payload;

namespace MadWizard.ARPergefactor.Neighborhood.Discovery
{
    internal class StaticNetworkDiscovery : IIEnumerable<Network> //, IDisposable TODO
    {
        private readonly IIPConfigurator IPConfigurator;

        private readonly List<Network> _networks = [];

        public StaticNetworkDiscovery(ILifetimeScope root, IIPConfigurator ip, ExpergefactorConfig config)
        {
            IPConfigurator = ip;

            foreach (var networkConfig in config.Network ?? [])
            {
                var network = RegisterNetwork(root, networkConfig);

                _networks.Add(network);
            }
        }

        private Network RegisterNetwork(ILifetimeScope root, NetworkConfig config)
        {
            var scopeNetwork = root.BeginLifetimeScope(MatchingScopeLifetimeTags.NetworkLifetimeScopeTag, builder =>
            {
                builder.RegisterType<Network>().As<IEthernetListener>()
                       .WithParameter(TypedParameter.From(config.Options))
                       .SingleInstance()
                       .AsSelf();

                builder.RegisterType<NetworkDevice>()
                       .WithParameter(TypedParameter.From(config.Interface))
                       .PropertiesAutowired(PropertyWiringOptions.AllowCircularDependencies)
                       .AsImplementedInterfaces()
                       .SingleInstance()
                       .AsSelf();

                RegisterRequestFilters(builder, config);
            });

            var network = scopeNetwork.Resolve<Network>();

            foreach (var configHost in config.Host ?? [])
                network.AddHost(RegisterHost(scopeNetwork, config, configHost));
            foreach (var configHost in config.Router ?? [])
                network.AddHost(RegisterHost(scopeNetwork, config, configHost));

            foreach (var configHost in config.WatchHost ?? [])
            {
                network.AddHost(RegisterHost(scopeNetwork, config, configHost));

                foreach (var configHostVirtual in configHost.VirtualHost ?? [])
                {
                    network.AddHost(RegisterVirtualHost(scopeNetwork, config, configHost, configHostVirtual));
                }
            }

            root.Disposer.AddInstanceForDisposal(scopeNetwork);

            return network;
        }

        private NetworkHost RegisterHost(ILifetimeScope scopeNetwork, NetworkConfig configNetwork, HostInfo config)
        {
            var scopeHost = scopeNetwork.BeginLifetimeScope(MatchingScopeLifetimeTags.NetworkHostLifetimeScopeTag, builder =>
            {
                var register = config switch
                {
                    PhysicalHostInfo configPhysical =>
                        builder.RegisterType<NetworkHost>().As<NetworkHost>()
                                .WithProperty(TypedParameter.From<PingMethod?>(configPhysical.MakePingMethod(configNetwork)))
                                .WithProperty(TypedParameter.From<PoseMethod?>(configPhysical.MakePoseMethod(configNetwork)))
                                .WithProperty(TypedParameter.From<WakeMethod?>(configPhysical.MakeWakeMethod(configNetwork)))
                            .SingleInstance()
                            .AsSelf(),

                    RouterInfo configRouter =>
                        builder.RegisterType<NetworkRouter>().As<NetworkHost>()
                            .WithParameter(TypedParameter.From(configRouter.Options))
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
            });

            var host = ConfigureHost(scopeHost, configNetwork, config);

            scopeNetwork.Disposer.AddInstanceForDisposal(scopeHost);

            return host;
        }

        private NetworkHost RegisterVirtualHost(ILifetimeScope scopeNetwork, NetworkConfig configNetwork, PhysicalHostInfo configPhysical, VirtualHostInfo config)
        {
            var scopeHost = scopeNetwork.BeginLifetimeScope(MatchingScopeLifetimeTags.NetworkHostLifetimeScopeTag, builder =>
            {
                builder.RegisterType<VirtualHost>().As<NetworkHost>()
                    .WithParameter(new TypedParameter(typeof(string), config.Name))
                    .WithParameter(NetworkHostParameter.FindBy(configPhysical.Name))
                    .WithProperty(TypedParameter.From<PingMethod?>(config.MakePingMethod(configNetwork)))
                    .WithProperty(TypedParameter.From<PoseMethod?>(config.MakePoseMethod(configNetwork)))
                    .WithProperty(TypedParameter.From<WakeMethod?>(config.MakeWakeMethod(configNetwork)))
                    .SingleInstance()
                    .AsSelf();

                RegisterRequestFilters(builder, config);
            });

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

            if ((config.Auto ?? configNetwork.Auto).HasFlag(AutoConfigType.IPv4) && !host.IPv4Addresses.Any())
                IPConfigurator.ConfigureIPv4(host);
            if ((config.Auto ?? configNetwork.Auto).HasFlag(AutoConfigType.IPv6) && !host.IPv6Addresses.Any())
                IPConfigurator.ConfigureIPv6(host);

            if (host.PoseMethod?.Latency is TimeSpan latency)
            {
                var imposter = scopeHost.Resolve<Imposter>();

                imposter.ConfigurePreemptiveImpersonation(latency);
            }

            return host;
        }

        private static void RegisterRequestFilters(ContainerBuilder builder, FilterScope scope)
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
                            .WithParameter(NetworkHostParameter.FindBy(filter.Name))
                            .As<HostFilterRule>()
                            .SingleInstance();
                    }
                    else
                    {
                        builder.RegisterType<StaticHostFilterRule>()
                            .WithParameter(TypedParameter.From(filter.Type))
                            .WithProperty(TypedParameter.From(filter.PhysicalAddress))
                            .WithProperty(TypedParameter.From(filter.IPAddresses.ToList()))
                            .As<HostFilterRule>()
                            .SingleInstance();
                    }
                }
            }

            RegisterServiceFilters(builder, scope);
            RegisterPingFilter(builder, scope);
        }

        private static void RegisterServiceFilters(ContainerBuilder builder, FilterScope scope)
        {
            // iterate over all service filter types
            IEnumerable<ServiceFilterRuleInfo> serviceFilters = (scope.ServiceFilterRule ?? [])
                .Concat(scope.HTTPFilterRule != null ? [scope.HTTPFilterRule] : []);

            foreach (var filter in serviceFilters)
            {
                var service = new TransportService(filter.Name, filter.Protocol, filter.Port);

                var register = builder.RegisterType<ServiceFilterRule>()
                    .WithParameter(TypedParameter.From(filter.Type))
                    .WithParameter(TypedParameter.From(service))
                    .SingleInstance()
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
                        payloadFilters.Add(new HTTPRequestFilterRule
                        {
                            Type = request.Type,
                            Path = request.Path,
                        });
                    }
                }

                register.WithProperty(TypedParameter.From(payloadFilters));
            }
        }

        private static void RegisterPingFilter(ContainerBuilder builder, FilterScope scope)
        {
            if (scope.PingFilterRule is PingFilterRuleInfo filter)
            {
                var register = builder.RegisterType<PingFilterRule>()
                    .WithParameter(TypedParameter.From(filter.Type))
                    .SingleInstance()
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

    file class NetworkHostParameter(string name) : ResolvedParameter(
        (pi, ctx) => pi.ParameterType == typeof(NetworkHost),
        (pi, ctx) => ctx.Resolve<Network>()[name])
    {
        internal static NetworkHostParameter FindBy(string name) => new(name);
    }

    file class HostFilterRuleParameter(HostFilterRuleInfo host) : ResolvedParameter(
        (pi, ctx) => pi.ParameterType == typeof(HostFilterRule), (pi, ctx) =>
        {
            if (host.IsDynamic)
            {
                return ctx.Resolve<DynamicHostFilterRule>(TypedParameter.From(host.Type),
                    NetworkHostParameter.FindBy(host.Name));
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
