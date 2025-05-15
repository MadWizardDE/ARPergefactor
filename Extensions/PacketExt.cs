using PacketDotNet.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;

namespace PacketDotNet
{
    internal static class PacketExt
    {
        public static PhysicalAddress? FindSourcePhysicalAddress(this Packet? packet)
        {
            if (packet?.Extract<EthernetPacket>() is EthernetPacket ethernet)
                return ethernet.SourceHardwareAddress;

            return null;
        }

        public static IPAddress? FindSourceIPAddress(this Packet? packet)
        {
            if (packet?.Extract<ArpPacket>() is ArpPacket arp)
                return arp.SenderProtocolAddress;
            if (packet?.Extract<IPPacket>() is IPPacket ip)
                return ip.SourceAddress;

            return null;
        }

        public static PhysicalAddress? FindDestinationPhysicalAddress(this Packet? packet)
        {
            if (packet?.Extract<EthernetPacket>() is EthernetPacket ethernet)
                return ethernet.DestinationHardwareAddress;

            return null;
        }

        public static IPAddress? FindDestinationIPAddress(this Packet? packet)
        {
            if (packet?.Extract<ArpPacket>() is ArpPacket arp)
                return arp.TargetProtocolAddress;
            if (packet?.Extract<IPPacket>() is IPPacket ip)
                return ip.DestinationAddress;

            return null;
        }

        public static string ToTraceString(this Packet packet)
        {
            //return packet.ToString(StringOutputType.Verbose);
            return packet.ToString(StringOutputType.Normal).Replace("][", "]\n\t[").Replace("[Ethernet", "\t[Ethernet");
        }

    }

    internal static class ArpPacketExt
    {
        public static bool IsAnnouncement(this ArpPacket arp)
        {
            return arp.Operation == ArpOperation.Request && arp.SenderProtocolAddress.Equals(arp.TargetProtocolAddress);
        }

        public static bool IsGratuitous(this ArpPacket arp)
        {
            return arp.Operation == ArpOperation.Response && arp.SenderProtocolAddress.Equals(arp.TargetProtocolAddress);
        }

        public static bool IsProbe(this ArpPacket arp)
        {
            return arp.Operation == ArpOperation.Request && arp.SenderProtocolAddress.Equals(IPAddress.Any);
        }
    }

    internal static class WakeOnLanPacketExt
    {
        public static WakeOnLanPacket WithPassword(this WakeOnLanPacket wol, byte[] password)
        {
            var bytes = new byte[wol.Bytes.Length + password.Length];
            System.Array.Copy(wol.Bytes, bytes, wol.Bytes.Length);

            return new WakeOnLanPacket(new ByteArraySegment(bytes))
            {
                Password = password
            };
        }
    }
}
