using MadWizard.ARPergefactor.Trigger;
using PacketDotNet;
using PacketDotNet.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.ARPergefactor.Packets
{
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
