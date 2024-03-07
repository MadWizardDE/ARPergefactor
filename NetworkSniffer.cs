using Autofac;
using MadWizard.ARPergefactor.Config;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SharpPcap;

namespace MadWizard.ARPergefactor
{
    internal class NetworkSniffer(IOptions<WakeConfig> config) : IStartable, IDisposable
    {
        public required ILogger<NetworkSniffer> Logger { private get; init; }

        public ILiveDevice? Device { get; private set; }

        public event EventHandler<PacketDotNet.Packet>? PacketReceived;

        void IStartable.Start()
        {
            foreach (var device in CaptureDeviceList.Instance)
            {
                if (device.Name.Contains(config.Value.Network.Interface))
                {
                    Device = device;

                    Device.Open();
                    Device.OnPacketArrival += Device_OnPacketArrival;
                    Device.StartCapture();

                    Logger.LogInformation($"Monitoring network interface \"{device.Description ?? device.Name}\"");

                    break;
                }
            }
        }

        private void Device_OnPacketArrival(object sender, PacketCapture capture)
        {
            var raw = capture.GetPacket();
            var packet = PacketDotNet.Packet.ParsePacket(raw.LinkLayerType, raw.Data);

            PacketReceived?.Invoke(this, packet);
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
