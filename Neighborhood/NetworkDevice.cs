using Autofac;
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
                var runtime = Device.Started;

                if (runtime)
                {
                    Logger.LogDebug("Changed BPF: '{expr}'", value);

                    Device.StopCapture();
                }

                Device.Filter = value;

                if (runtime)
                {
                    Device.StartCapture();
                }
            }
        }
        public IEnumerable<IPacketFilter> Filters { private get; init; } = [];

        private ILiveDevice Device { get; set; }
        public readonly bool IsMaxResponsiveness;
        public readonly bool IsNoCaptureLocal;

        public event EventHandler<EthernetPacket>? EthernetCaptured;

        public PhysicalAddress PhysicalAddress => Device?.MacAddress!;

        public IPAddress? IPv4Address
        {
            get
            {
                if (Device is LibPcapLiveDevice pcap)
                {
                    return pcap.Addresses.Where(address => address.Addr.ipAddress?.AddressFamily == AddressFamily.InterNetwork).SingleOrDefault()?.Addr.ipAddress;
                }

                return null;
            }
        }
        public IPAddress? IPv6LinkLocalAddress
        {
            get
            {
                if (Device is LibPcapLiveDevice pcap)
                {
                    return pcap.Addresses.Where(address => address.Addr.ipAddress?.AddressFamily == AddressFamily.InterNetworkV6 && address.Addr.ipAddress.IsIPv6LinkLocal).SingleOrDefault()?.Addr.ipAddress;
                }

                return null;
            }
        }

        public NetworkInterface Interface { get; private init; }

        public NetworkDevice(ILogger<NetworkDevice> logger, string interfaceName)
        {
            Logger = logger;

            foreach (var device in CaptureDeviceList.Instance)
            {
                if (MatchDeviceName(device, interfaceName))
                {
                    if (TryOpen(device, ref IsMaxResponsiveness, ref IsNoCaptureLocal))
                    {
                        CheckDeviceCapabilities(Device = device); // TODO do we need to be more resilient here? What happens, when the device changes IP address?

                        Interface = NetworkInterface.GetAllNetworkInterfaces().Where(ni => ni.Name == Name).First();

                        return;
                    }
                    else
                    {
                        throw new Exception($"Failed to open network interface \"{device.Description ?? device.Name}\"");
                    }
                }
            }

            throw new FileNotFoundException($"Network interface with name like '{interfaceName}' not found.");
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

            Logger.LogInformation($"Monitoring network interface \"{Name}\", MAC={PhysicalAddress?.ToHexString()}, IPv4={IPv4Address?.ToString()} [{string.Join(", ", features)}]");

            Logger.LogDebug("Using BPF: '{expr}'", Filter);
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
                Logger.LogInformation($"Stopped monitoring of network interface \"{Name}\"");

                Device.StopCapture();
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

        private static void CheckDeviceCapabilities(ILiveDevice device)
        {
            if (device.MacAddress == null)
            {
                throw new Exception($"Cannot use network interface \"{device.Description ?? device.Name}\": No MAC address.");
            }

            if (device is LibPcapLiveDevice pcap)
            {
                if (!pcap.Addresses.Where(address => address.Addr.ipAddress?.AddressFamily == AddressFamily.InterNetwork).Any())
                {
                    throw new Exception($"Cannot use network interface \"{device.Description ?? device.Name}\": No IPv4 address.");
                }
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
                Logger.LogDebug($"Device \"{device.Description ?? device.Name}\" does not support NoCaptureLocal mode.");
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
                Logger.LogDebug($"Device \"{device.Description ?? device.Name}\" does not support MaxResponsiveness mode.");
            }

            maxResponsiveness = false; // not supported

            try
            {
                device.Open(DeviceModes.Promiscuous);

                return true;
            }
            catch (PcapException)
            {
                Logger.LogError($"Device \"{device.Description ?? device.Name}\" does not support Promiscuous mode.");
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
