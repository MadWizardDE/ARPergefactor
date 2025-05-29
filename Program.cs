using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Xml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Autofac;
using Autofac.Extensions.DependencyInjection;

using MadWizard.ARPergefactor;
using MadWizard.ARPergefactor.Config;
using MadWizard.ARPergefactor.Logging;
using MadWizard.ARPergefactor.Neighborhood;
using MadWizard.ARPergefactor.Impersonate;
using MadWizard.ARPergefactor.Request.Filter;
using MadWizard.ARPergefactor.Trigger;
using MadWizard.ARPergefactor.Request;
using Autofac.Builder;
using MadWizard.ARPergefactor.Impersonate.ARP;
using MadWizard.ARPergefactor.Impersonate.NDP;
using MadWizard.ARPergefactor.Neighborhood.Discovery;
using MadWizard.ARPergefactor.Neighborhood.Cache;
using System.Runtime.InteropServices;
using NLog;
using NLog.Extensions.Logging;

using LogLevel = Microsoft.Extensions.Logging.LogLevel;
using System.Collections.Generic;


static IHostBuilder CreateHostBuilder(string[] args) => Host.CreateDefaultBuilder(args)

// Konfiguration laden
    .ConfigureAppConfiguration((ctx, builder) =>
    {
        XmlConfigurationSource source =
            new CustomXmlConfigurationSource("config.xml", optional: false, reloadOnChange: true)
                .AddNamelessCollectionElements("Network", "RequestFilterRule")
                .AddBooleanAttribute("must", new() { ["type"] = "Must" })
                .AddEnumAttribute("autoDetect");

        builder.Add(source);
        builder.AddCommandLine(args);
    })

    // Logging
    .ConfigureLogging(logging =>
    {
        logging.ClearProviders();
        logging.SetMinimumLevel(LogLevel.Trace);
        //logging.AddConsole();
        logging.AddNLog();
    })

    .UseServiceProviderFactory(new AutofacServiceProviderFactory())
    .ConfigureContainer<ContainerBuilder>((ctx, builder) =>
    {
        var config = ctx.Configuration.Get<ExpergefactorConfig>(opt => opt.BindNonPublicProperties = true)!;

        builder.RegisterInstance(config).AsSelf().SingleInstance();

        // Network Discovery and Configuration
        builder.RegisterType<StaticNetworkDiscovery>()
            .AsImplementedInterfaces()
            .SingleInstance();
        builder.RegisterType<PeriodicIPConfigurator>()
            .WithParameter(TypedParameter.From(config.AutoMethod))
            .AsImplementedInterfaces()
            .SingleInstance();

        // TODO: can there be other methods?

        // Main Service Component
        builder.RegisterType<KnockerUp>()
            .AsImplementedInterfaces()
            .SingleInstance()
            .AsSelf();
        builder.RegisterType<WakeLogger>()
            .SingleInstance()
            .AsSelf();


        // --- Network Scope ---- //

        //builder.RegisterType<Network>()
        //    .InstancePerNetwork()
        //    .AsSelf();

        //builder.RegisterType<NetworkDevice>()
        //    .AsImplementedInterfaces()
        //    .InstancePerNetwork()
        //    .AsSelf();

        builder.RegisterType<LocalPacketFilter>()
            .AsImplementedInterfaces()
            .InstancePerNetwork();

        // Different implementations of local IP caches
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            builder.RegisterType<WindowsNeighborCache>()
                .InstancePerNetwork()
                .As<ILocalIPCache>();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            builder.RegisterType<LinuxNeighborCache>()
                .InstancePerNetwork()
                .As<ILocalIPCache>();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            builder.RegisterType<LocalARPCache>()
                .InstancePerNetwork()
                .AsSelf();
            builder.RegisterType<LocalNDPCache>()
                .InstancePerNetwork()
                .AsSelf();
            builder.RegisterType<MacOSNeighborCache>()
                .InstancePerNetwork()
                .As<ILocalIPCache>();
        }

        // Triggers
        builder.RegisterType<WakeOnIP>()
            .AsImplementedInterfaces()
            .InstancePerNetwork();
        builder.RegisterType<WakeOnARP>()
            .AsImplementedInterfaces()
            .InstancePerNetwork();
        builder.RegisterType<WakeOnNDP>()
            .AsImplementedInterfaces()
            .InstancePerNetwork();
        builder.RegisterType<WakeOnWOL>()
            .AsImplementedInterfaces()
            .InstancePerNetwork();

        // --- NetworkHost Scope ---- //

        // Impersonation
        builder.RegisterType<Imposter>()
            .AsImplementedInterfaces()
            .OnActivated((args) => args.Instance.ConfigureImpersonation())
            .InstancePerNetworkHost()
            .AsSelf();
        builder.RegisterType<ARPImpersonation>()
            .AsImplementedInterfaces()
            .InstancePerDependency()
            .AsSelf();
        builder.RegisterType<NDPImpersonation>()
            .AsImplementedInterfaces()
            .InstancePerDependency()
            .AsSelf();

        // --- Request Scope ---- //

        builder.RegisterType<WakeRequest>()
            .PropertiesAutowired(PropertyWiringOptions.AllowCircularDependencies)
            .InstancePerRequest()
            .AsSelf();

        // Host Filters
        builder.RegisterType<HostFilter>()
            .AsImplementedInterfaces()
            .InstancePerRequest();
        builder.RegisterType<VirtualHostFilter>()
            .AsImplementedInterfaces()
            .InstancePerRequest();
        builder.RegisterType<RouterFilter>()
            .AsImplementedInterfaces()
            .InstancePerRequest();
        // IP Filters
        builder.RegisterType<ServiceFilter>()
            .AsImplementedInterfaces()
            .InstancePerRequest();
        builder.RegisterType<PingFilter>()
            .AsImplementedInterfaces()
            .InstancePerRequest();
    })

    // Dynamic Services
    .ConfigureServices((ctx, services) =>
    {
        services.Configure<ExpergefactorConfig>(ctx.Configuration, opt => opt.BindNonPublicProperties = true);
    })
;

await CreateHostBuilder(args).RunConsoleAsync();

namespace MadWizard.ARPergefactor
{
    public static class MatchingScopeLifetimeTags
    {
        public static readonly object NetworkLifetimeScopeTag = "Network";
        public static readonly object NetworkHostLifetimeScopeTag = "NetworkHost";
        public static readonly object RequestLifetimeScopeTag = Autofac.Core.Lifetime.MatchingScopeLifetimeTags.RequestLifetimeScopeTag;

        public static IRegistrationBuilder<TLimit, TActivatorData, TStyle>InstancePerNetwork<TLimit, TActivatorData, TStyle>(
                this IRegistrationBuilder<TLimit, TActivatorData, TStyle> registration, params object[] lifetimeScopeTags)
        {
            ArgumentNullException.ThrowIfNull(registration, nameof(registration));

            var tags = new[] { NetworkLifetimeScopeTag }.Concat(lifetimeScopeTags).ToArray();

            return registration.InstancePerMatchingLifetimeScope(tags);
        }

        public static IRegistrationBuilder<TLimit, TActivatorData, TStyle> InstancePerNetworkHost<TLimit, TActivatorData, TStyle>(
        this IRegistrationBuilder<TLimit, TActivatorData, TStyle> registration, params object[] lifetimeScopeTags)
        {
            ArgumentNullException.ThrowIfNull(registration, nameof(registration));

            var tags = new[] { NetworkHostLifetimeScopeTag }.Concat(lifetimeScopeTags).ToArray();

            return registration.InstancePerMatchingLifetimeScope(tags);
        }
    }
}