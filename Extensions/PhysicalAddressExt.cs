﻿using System.Runtime.InteropServices;

namespace System.Net.NetworkInformation
{
    internal static class PhysicalAddressExt
    {
        public static PhysicalAddress Empty = PhysicalAddress.Parse("00:00:00:00:00:00");
        public static PhysicalAddress Broadcast = PhysicalAddress.Parse("FF:FF:FF:FF:FF:FF");

        public static string ToHexString(this PhysicalAddress address)
        {
            return string.Join(":", (from z in address.GetAddressBytes() select z.ToString("X2")).ToArray());
        }

        public static string ToPlatformString(this PhysicalAddress address)
        {
            var format = address.ToHexString();

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                format = format.Replace(":", "-");

            return format;
        }
    }
}
