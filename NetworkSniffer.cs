using ARPergefactor.Packet;
using Autofac;
using MadWizard.ARPergefactor.Config;
using MadWizard.ARPergefactor.Request;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PacketDotNet;
using SharpPcap;
using SharpPcap.LibPcap;
using System.Net;
using System.Net.NetworkInformation;

namespace MadWizard.ARPergefactor
{
    internal class NetworkSniffer(IOptions<WakeConfig> config) : IStartable, IDisposable
    {
        public required ILogger<NetworkSniffer> Logger { private get; init; }

        private IList<Packet> _sentPackets = [];

        private ILiveDevice? Device { get; set; }

        private bool IsMaxResponsiveness { get; set; }
        private bool IsNoCaptureLocal { get; set; }

        public PhysicalAddress? PhysicalAddress => Device?.MacAddress;
        public IPAddress? IPv4Address
        {
            get
            {
                if (Device is LibPcapLiveDevice pcap)
                {
                    return pcap.Addresses.Where(address => address.Addr.ipAddress?.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork).SingleOrDefault()?.Addr.ipAddress;
                }

                return null;
            }
        }

        public event EventHandler<Packet>? PacketReceived;

        void IStartable.Start()
        {
            foreach (var device in CaptureDeviceList.Instance)
            {
                if (device.Name.Contains(config.Value.Network.Interface))
                {
                    if (TryOpen(device, out bool maxResponsiveness, out bool noCaptureLocal))
                    {
                        Device = device;

                        Device.OnPacketArrival += Device_OnPacketArrival;
                        Device.StartCapture();

                        List<string> features = [];
                        if (IsMaxResponsiveness = maxResponsiveness)
                            features.Add("MaxResponsiveness");
                        if (IsNoCaptureLocal = noCaptureLocal)
                            features.Add("NoCaptureLocal");

                        Logger.LogInformation($"Monitoring network interface \"{device.Description ?? device.Name}\", MAC={PhysicalAddress?.ToHexString()}, IPv4={IPv4Address?.ToString()} [{string.Join(", ", features)}]");

                        break;
                    }
                    else
                    {
                        Logger.LogError($"Failed to start open network interface \"{device.Description ?? device.Name}\"");
                    }
                }
            }
        }

        private bool TryOpen(ILiveDevice device, out bool maxResponsiveness, out bool noCaptureLocal)
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

            }

            try
            {
                device.Open(DeviceModes.Promiscuous | DeviceModes.MaxResponsiveness);

                maxResponsiveness = true;
                noCaptureLocal = false;

                return true;
            }
            catch (PcapException)
            {

            }

            try
            {
                device.Open(DeviceModes.Promiscuous);

                maxResponsiveness = false;
                noCaptureLocal = false;

                return true;
            }
            catch (PcapException)
            {
                maxResponsiveness = false;
                noCaptureLocal = false;

                return false;
            }
        }

        private void Device_OnPacketArrival(object sender, PacketCapture capture)
        {
            if (!FilterInjectedPacket(capture))
            {
                var raw = capture.GetPacket();
                var packet = Packet.ParsePacket(raw.LinkLayerType, raw.Data);

                PacketReceived?.Invoke(this, packet);
            }
        }

        private bool FilterInjectedPacket(PacketCapture capture)
        {
            lock (_sentPackets)
                foreach (var sent in _sentPackets)
                    if (capture.Data.SequenceEqual(sent.Bytes))
                        return _sentPackets.Remove(sent);

            return false;
        }

        public void SendPacket(Packet packet)
        {
            Device.SendPacket(packet);

            if (!IsNoCaptureLocal)
                lock (_sentPackets)
                    _sentPackets.Add(packet);
        }

        void IDisposable.Dispose()
        {
            if (Device != null)
            {
                Device.StopCapture();
                Device.Close();
                Device.Dispose();
                Device = null;
            }
        }
    }
}
