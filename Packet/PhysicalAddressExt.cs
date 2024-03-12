using MadWizard.ARPergefactor.Request;
using PacketDotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;

namespace ARPergefactor.Packet
{
    internal static class PhysicalAddressExt
    {
        public static PhysicalAddress Empty = PhysicalAddress.Parse("00:00:00:00:00:00");
        public static PhysicalAddress Broadcast = PhysicalAddress.Parse("FF:FF:FF:FF:FF:FF");

        public static string ToHexString(this PhysicalAddress address)
        {
            return string.Join(":", (from z in address.GetAddressBytes() select z.ToString("X2")).ToArray());
        }
    }
}
