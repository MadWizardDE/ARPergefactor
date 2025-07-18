﻿using Autofac;
using MadWizard.ARPergefactor.Neighborhood.Filter;
using Microsoft.Extensions.Logging;
using PacketDotNet;
using SharpPcap;
using SharpPcap.LibPcap;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace MadWizard.ARPergefactor.Neighborhood
{
    public class NetworkDevice : IDisposable
    {
        public string Name => Device is PcapDevice pcap ? pcap.Interface.FriendlyName : Device.Name;

        public ILogger<NetworkDevice> Logger { private get; init; }

        public string Filter
        {
            get => Device.Filter;

            set
            {
                if (Device.Filter != value)
                {
                    var runtime = Device.Started;

                    if (runtime)
                    {
                        Logger.LogDebug("BPF rule -> '{expr}'", value);

                        Device.StopCapture();
                    }

                    Device.Filter = value;

                    if (runtime)
                    {
                        Device.StartCapture();
                    }
                }
            }
        }
        public IEnumerable<IPacketFilter> Filters { private get; init; } = [];

        private ILiveDevice Device { get; set; }
        public readonly bool IsMaxResponsiveness;
        public readonly bool IsNoCaptureLocal;

        public event EventHandler<EthernetPacket>? EthernetCaptured;

        public PhysicalAddress PhysicalAddress => Device?.MacAddress ?? PhysicalAddress.None;

        public IEnumerable<IPAddress> IPAddresses
        {
            get
            {
                IEnumerable<IPAddress> pcapAddresses = [];
                IEnumerable<IPAddress> niAddresses = [];

                if (Device is LibPcapLiveDevice pcap)
                {
                    pcapAddresses = pcap.Addresses
                        .Where(address => address.Addr.ipAddress is not null)
                        .Select(address => address.Addr.ipAddress);
                }

                niAddresses = Interface.GetIPProperties().UnicastAddresses
                    .Where(unicast => unicast.Address is not null)
                    .Select(unicast => unicast.Address);

                return pcapAddresses.Concat(niAddresses).Distinct();
            }
        }

        public IPAddress? IPv4Address => IPAddresses.Where(ip => ip.AddressFamily == AddressFamily.InterNetwork).FirstOrDefault();
        public IPAddress? IPv6LinkLocalAddress => IPAddresses.Where(ip => ip.AddressFamily == AddressFamily.InterNetworkV6 && ip.IsIPv6LinkLocal).FirstOrDefault();
        public IEnumerable<IPAddress> IPv6Addresses => IPAddresses.Where(ip => ip.AddressFamily == AddressFamily.InterNetworkV6);

        public NetworkInterface Interface { get; private set; }

        public bool IsCapturing => Device.Started;

        public NetworkDevice(ILogger<NetworkDevice> logger, string interfaceName)
        {
            Logger = logger;

            foreach (var device in CaptureDeviceList.Instance)
            {
                if (MatchDeviceName(device, interfaceName))
                {
                    if (TryOpen(device, ref IsMaxResponsiveness, ref IsNoCaptureLocal))
                    {
                        Device = device;

                        Interface = ReloadInterface();

                        CheckDeviceCapabilities(device); // TODO do we need to be more resilient here? What happens, when the device changes IP address?

                        return;
                    }
                    else
                    {
                        throw new Exception($"Failed to open network interface \"{device.Description ?? device.Name}\"");
                    }
                }
            }

            throw new FileNotFoundException($"Network interface with name like \"{interfaceName}\" not found.");
        }

        public NetworkInterface ReloadInterface()
        {
            return Interface = NetworkInterface.GetAllNetworkInterfaces().Where(ni => ni.Name == Name).First();
        }

        public void StartCapture()
        {
            Device.OnPacketArrival += Device_OnPacketArrival;
            Device.StartCapture();

            List<string> features = [];
            if (IsMaxResponsiveness)
                features.Add("MaxResponsiveness");
            if (IsNoCaptureLocal)
                features.Add("NoCaptureLocal");

            var countIPv6 = IPv6Addresses.Count();

            Logger.LogInformation($"Monitoring network interface \"{Name}\"; MAC={PhysicalAddress?.ToHexString()}, IPv4={IPv4Address?.ToString() ?? "?"}" +
                (countIPv6 > 0 ? $", IPv6={IPv6LinkLocalAddress?.ToString() ?? IPv6Addresses.FirstOrDefault()?.ToString() ?? "?"}" + (countIPv6 - 1 > 0 ? $"(+{countIPv6-1})" : "") : "") +
                $" [{string.Join(", ", features)}]");

            Logger.LogDebug("BPF rule = '{expr}'", Filter);
        }

        private void Device_OnPacketArrival(object sender, PacketCapture capture)
        {
            try
            {
                if (!FilterInjectedPacket(capture))
                {
                    var raw = capture.GetPacket();

                    if (Packet.ParsePacket(raw.LinkLayerType, raw.Data) is EthernetPacket ethernet)
                    {
                        if (Logger.IsEnabled(LogLevel.Trace)) 
                        {
                            //Logger.LogTrace($"RECEIVED PACKET\n{ethernet.ToTraceString()}");
                        }

                        try
                        {
                            EthernetCaptured?.Invoke(this, ethernet);
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError(ex, "Error processing packet:\n{packet}", ethernet);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error while filtering/parsing packet."); // low level error
            }
        }

        private bool FilterInjectedPacket(PacketCapture capture)
        {
            foreach (var filter in Filters)
            {
                if (filter.FilterIncoming(capture))
                {
                    return true;
                }
            }

            return false;
        }

        public void SendPacket(EthernetPacket packet)
        {
            if (!Filters.Select(filter => filter.FilterOutgoing(packet)).Where(f => f == true).Any())
            {
                if (Logger.IsEnabled(LogLevel.Trace))
                {
                    Logger.LogTrace($"SEND PACKET\n{packet.ToTraceString()}");
                }

                Device.SendPacket(packet);
            }
        }

        public void StopCapture()
        {
            if (Device.Started)
            {
                Device.StopCapture();

                Logger.LogInformation($"Stopped monitoring of network interface \"{Name}\"");
            }
        }

        private static bool MatchDeviceName(ILiveDevice device, string name)
        {
            if (device is PcapDevice pcap)
            {
                if (pcap.Interface?.Name?.Contains(name) ?? false)
                    return true;
                if (pcap.Interface?.Description?.Contains(name) ?? false)
                    return true;
                if (pcap.Interface?.FriendlyName?.Contains(name) ?? false)
                    return true;
            }
            else
            {
                if (device.Name?.Contains(name) ?? false)
                    return true;
            }

            return false;
        }

        private void CheckDeviceCapabilities(ILiveDevice device)
        {
            //if (device.MacAddress == null)
            //{
            //    throw new Exception($"Cannot use network interface '{device.Description ?? device.Name}': No MAC address.");
            //}


            if (device is LibPcapLiveDevice pcap)
            {
                if (IPv4Address is null)
                {
                    throw new Exception($"Cannot use network interface '{device.Description ?? device.Name}': No IPv4 address.");
                }

                Logger.LogDebug("Listing addresses for device '{deviceName}'...", device.Description ?? device.Name);

                Logger.LogDebug("{family} '{address}'", "MAC", PhysicalAddress.ToHexString());

                foreach (var ip in IPAddresses)
                    Logger.LogDebug("{family} '{address}'", ip.ToFamilyName(), ip);
            }
        }

        private bool TryOpen(ILiveDevice device, ref bool maxResponsiveness, ref bool noCaptureLocal)
        {
            try
            {
                device.Open(DeviceModes.Promiscuous | DeviceModes.MaxResponsiveness | DeviceModes.NoCaptureLocal);

                maxResponsiveness = true;
                noCaptureLocal = true;

                return true;
            }
            catch (PcapException)
            {
                Logger.LogDebug($"Device '{device.Description ?? device.Name}' does not support NoCaptureLocal mode. Compensating with fallback buffer.");
            }

            noCaptureLocal = false; // not supported

            try
            {
                device.Open(DeviceModes.Promiscuous | DeviceModes.MaxResponsiveness);

                maxResponsiveness = true;

                return true;
            }
            catch (PcapException)
            {
                Logger.LogWarning($"Device '{device.Description ?? device.Name}' does not support MaxResponsiveness mode. Anticipate slow application behavior.");
            }

            maxResponsiveness = false; // not supported

            try
            {
                device.Open(DeviceModes.Promiscuous);

                return true;
            }
            catch (PcapException)
            {
                Logger.LogError($"Device '{device.Description ?? device.Name}' does not support Promiscuous mode.");
            }

            return false; // at least promiscuous mode is needed
        }

        void IDisposable.Dispose()
        {
            StopCapture();

            Device.Close();
            Device.Dispose();
        }
    }

    /// <summary>
    /// This Filter prevent packets sent by us, being processed as incoming packets again,
    /// if the device is cannot do this by itself.
    /// </summary>
    /// 
    /// <param name="device">the monitored network device</param>
    internal class LocalPacketFilter(NetworkDevice device) : IPacketFilter
    {
        private readonly IList<byte[]> _sentPackets = [];

        public bool FilterIncoming(PacketCapture packet)
        {
            if (device.IsNoCaptureLocal)
                return false;

            lock (_sentPackets)
            {
                foreach (var bytes in _sentPackets)
                    if (packet.Data.SequenceEqual(bytes))
                        return _sentPackets.Remove(bytes);
            }

            return false;
        }

        public bool FilterOutgoing(Packet packet)
        {
            if (device.IsNoCaptureLocal)
                return false;

            lock (_sentPackets)
            {
                _sentPackets.Add(packet.Bytes);
            }

            return false;
        }
    }

    internal class SimulationPacketFilter : IPacketFilter
    {
        public required ILogger<SimulationPacketFilter> Logger { private get; init; }

        public bool FilterIncoming(PacketCapture packet) => false;

        public bool FilterOutgoing(Packet packet) => true;
    }
}
