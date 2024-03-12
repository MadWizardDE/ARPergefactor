using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Xml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Autofac;
using Autofac.Extensions.DependencyInjection;

using MadWizard.ARPergefactor;
using MadWizard.ARPergefactor.Config;
using MadWizard.ARPergefactor.Filter;
using MadWizard.ARPergefactor.Trigger;
using MadWizard.ARPergefactor.Logging;
using Microsoft.Extensions.Logging.Console;
using ARPergefactor;
using System.Net.NetworkInformation;
using System.Net;
using ARPergefactor.Filter.MadWizard.ARPergefactor.Filter;
using System.Diagnostics;
using System.Security.Cryptography;
using SharpPcap;

static IHostBuilder CreateHostBuilder(string[] args) => Host.CreateDefaultBuilder(args)

    // Konfiguration laden
    .ConfigureAppConfiguration((ctx, builder) =>
    {
        builder.AddCustomXmlFile("config.xml", optional: false, reloadOnChange: true);
        builder.AddCommandLine(args);
    })

    // Logging
    .ConfigureLogging((logging) =>
    {
        //logging.AddSimpleConsole(options =>
        //{
        //    //options.TimestampFormat = "dd/MM/yyyy HH:mm:ss ";
        //    options.IncludeScopes = true;
        //    options.SingleLine = true;
        //});

        logging.AddConsole(options => options.FormatterName = "arp");
        logging.AddConsoleFormatter<CustomLogFormatter, ConsoleFormatterOptions>();

        //logging.SetMinimumLevel(LogLevel.Debug);
    })

    .UseServiceProviderFactory(new AutofacServiceProviderFactory())
    .ConfigureContainer<ContainerBuilder>((ctx, builder) =>
    {

        builder.RegisterType<NetworkSniffer>()
            .AsImplementedInterfaces()
            .SingleInstance()
            .AsSelf();

        builder.RegisterType<HeartbeatMonitor>()
            .AsImplementedInterfaces()
            .SingleInstance()
            .AsSelf();

        builder.RegisterType<Imposter>()
            .AsImplementedInterfaces()
            .SingleInstance()
            .AsSelf();

        // Triggers
        builder.RegisterType<WakeOnARP>()
            .AsImplementedInterfaces()
            .SingleInstance();
        builder.RegisterType<WakeOnWOL>()
            .AsImplementedInterfaces()
            .SingleInstance();

        // Passive Filters
        builder.RegisterType<BlacklistHostFilter>()
            .AsImplementedInterfaces()
            .SingleInstance();
        builder.RegisterType<WhitelistHostFilter>()
            .AsImplementedInterfaces()
            .SingleInstance();
        builder.RegisterType<RoutersFilter>()
            .AsImplementedInterfaces()
            .SingleInstance();
        builder.RegisterType<SelfFilter>()
            .AsImplementedInterfaces()
            .SingleInstance();
        // Active Filters
        builder.RegisterType<PingFilter>()
            .AsImplementedInterfaces()
            .SingleInstance();
        builder.RegisterType<ServiceFilter>()
            .AsImplementedInterfaces()
            .SingleInstance();

    })

    // Dynamic Services
    .ConfigureServices((ctx, services) =>
    {
        services.Configure<WakeConfig>(ctx.Configuration, opt => opt.BindNonPublicProperties = true);

        services.AddHostedService<KnockerUp>();
    })

    .UseConsoleLifetime()
;

await CreateHostBuilder(args).RunConsoleAsync();
