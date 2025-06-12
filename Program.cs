using Autofac;
using Autofac.Builder;
using Autofac.Extensions.DependencyInjection;
using MadWizard.ARPergefactor;
using MadWizard.ARPergefactor.Config;
using MadWizard.ARPergefactor.Impersonate;
using MadWizard.ARPergefactor.Impersonate.Protocol;
using MadWizard.ARPergefactor.Neighborhood;
using MadWizard.ARPergefactor.Neighborhood.Cache;
using MadWizard.ARPergefactor.Neighborhood.Discovery;
using MadWizard.ARPergefactor.Neighborhood.Filter;
using MadWizard.ARPergefactor.Reachability;
using MadWizard.ARPergefactor.Wake;
using MadWizard.ARPergefactor.Wake.Filter;
using MadWizard.ARPergefactor.Wake.Trigger;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Xml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using NLog;
using NLog.Config;
using NLog.Extensions.Logging;
using NLog.Targets;
using System.Data;
using System.Runtime.InteropServices;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

const string LINUX_CONFIG_PATH = "/etc/arpergefactor";

static IHostBuilder CreateHostBuilder(string[] args)
{
    bool useFHS = false; // use Filesystem Hierarchy Standard? (for Linux systems)

    string configPath = "config.xml";

    if (Path.Exists(LINUX_CONFIG_PATH))
    {
        configPath = Path.Combine(LINUX_CONFIG_PATH, "config.xml");
        var configNLogPath = Path.Combine(LINUX_CONFIG_PATH, "NLog.config");

        if (Path.Exists(configNLogPath))
        {
            LogManager.Configuration = new XmlLoggingConfiguration(configNLogPath);
        }

        useFHS = true;
    }

    return Host.CreateDefaultBuilder(args)

        // Load configuration
        .ConfigureAppConfiguration((ctx, builder) =>
        {
            XmlConfigurationSource source =
                new CustomXmlConfigurationSource(configPath, optional: false, reloadOnChange: true)
                    .AddNamelessCollectionElement("Network")
                    .AddNamelessCollectionElement("RequestFilterRule")
                    .AddBooleanAttribute("must", new() { ["type"] = "Must" })
                    .AddEnumAttribute("autoDetect")
                    .AddEnumAttribute("wakeRedirect");

            builder.Add(source);
            builder.AddCommandLine(args);
        })

        //Configure logging
        .ConfigureLogging(logging =>
        {
            logging.ClearProviders();

            logging.SetMinimumLevel(LogLevel.Trace);

            // Fallback if no config file has been found
            if (LogManager.Configuration is LoggingConfiguration config)
            {
                if (!config.Variables.ContainsKey("logDir"))
                {
                    config.Variables["logDir"] = useFHS
                        // Linux filesystem layout
                        ? "/var/log/arpergefactor"
                        // Portable mode
                        : "${currentdir:dir=logs}";
                }
            }
            else
            {
                config = new LoggingConfiguration();
            }

            LogManager.ConfigurationChanged += (sender, args) =>
            {
                if (args.ActivatedConfiguration is LoggingConfiguration configNew && !configNew.HasConsoleTarget())
                {
                    var target = new ConsoleTarget("console")
                    {
                        Layout = "${pad:padding=5:inner=${level:uppercase=true}} :: ${message} ${exception}"
                    };

                    configNew.AddRule(NLog.LogLevel.Info, NLog.LogLevel.Fatal, target, "MadWizard.ARPergefactor.*");

                    LogManager.Configuration = configNew;
                }
            };

            LogManager.Configuration = config;

            logging.AddNLog();
        })

        .UseServiceProviderFactory(new AutofacServiceProviderFactory())
        .ConfigureContainer<ContainerBuilder>((ctx, builder) =>
        {
            var config = ctx.Configuration.Get<ExpergefactorConfig>(opt => opt.BindNonPublicProperties = true)!;

            if (config.Version != 2)
            {
                throw new NotSupportedException($"Unsupported configuration version: {config.Version}");
            }

            //builder.RegisterInstance(config).AsSelf().SingleInstance();

            // Network Discovery and Configuration
            builder.RegisterType<StaticNetworkDiscovery>()
                .AsImplementedInterfaces()
                .SingleInstance();

            // Main Service Component
            builder.RegisterType<KnockerUp>()
                .AsImplementedInterfaces()
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

            builder.RegisterType<BPFTrafficShaper>()
                .InstancePerNetwork()
                .AsSelf();
            builder.RegisterType<TrafficShapeRequest>()
                .InstancePerDependency()
                .AsSelf();

            builder.RegisterType<LocalPacketFilter>()
                .AsImplementedInterfaces()
                .InstancePerNetwork();

            // Network Services
            builder.RegisterType<ImpersonationService>()
                .AsImplementedInterfaces()
                .InstancePerNetwork()
                .AsSelf();
            builder.RegisterType<ReachabilityService>()
                .AsImplementedInterfaces()
                .InstancePerNetwork()
                .AsSelf();
            builder.RegisterType<WakeService>()
                .AsImplementedInterfaces()
                .InstancePerNetwork()
                .AsSelf();

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

            // Impersonation protocols
            builder.RegisterType<ARPImpersonation>()
                .AsImplementedInterfaces()
                .InstancePerDependency()
                .AsSelf();
            builder.RegisterType<NDPImpersonation>()
                .AsImplementedInterfaces()
                .InstancePerDependency()
                .AsSelf();

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

            // --- NetworkHost Scope ---- //

            // Impersonation helper
            builder.RegisterType<Imposter>()
                .AsImplementedInterfaces()
                .InstancePerNetworkHost()
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
        });
}

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

    public static class LoggingConfigurationExtensions
    {
        public static bool HasConsoleTarget(this LoggingConfiguration config)
        {
            foreach (var target in config.AllTargets)
            {
                if (target is ConsoleTarget consoleTarget)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
