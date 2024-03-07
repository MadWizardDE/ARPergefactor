using MadWizard.ARPergefactor.Config;
using MadWizard.ARPergefactor.Request;
using PacketDotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.ARPergefactor.Trigger
{
    internal interface IWakeTrigger
    {
        string MethodName { get; }

        WakeRequest? AnalyzeNetworkPacket(NetworkConfig network, EthernetPacket ethernet);
    }
}
