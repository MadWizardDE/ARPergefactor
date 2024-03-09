using MadWizard.ARPergefactor.Config;
using Microsoft.Extensions.Logging;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.ARPergefactor.Request
{
    internal class WakeRequest
    {
        private readonly NetworkConfig _network;
        private readonly Stack<WakeHostInfo> _hosts = new();

        internal WakeRequest(NetworkConfig network, WakeHostInfo info, bool observed = false)
        {
            _network = network;
            AddHost(info);

            WasObserved = observed;
        }

        public bool WasObserved { get; init; }

        public PhysicalAddress? SourcePhysicalAddress { get; set; }
        public IPAddress? SourceIPAddress { get; set; }

        public NetworkConfig Network => this._network;
        public IEnumerable<WakeHostInfo> Hosts => this._hosts;
        public WakeHostInfo RequestedHost => _hosts.Last();
        public WakeHostInfo TargetHost => _hosts.First();

        public WakeRequest AddHost(WakeHostInfo info)
        {
            _hosts.Push(info);

            return this;
        }

        public override string ToString()
        {
            if (RequestedHost != TargetHost)
            {
                return ($"Magic Packet for \"{RequestedHost.Name}\" to \"{TargetHost.Name}\"");
            }
            else
            {
                return ($"Magic Packet to \"{TargetHost.Name}\"");
            }
        }
    }
}
