using MadWizard.ARPergefactor.Config;
using Microsoft.Extensions.Logging;
using PacketDotNet;
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
    internal class WakeRequest : Request
    {
        private readonly NetworkConfig _network;
        private readonly Stack<WakeHostInfo> _hosts = new();
        private readonly bool _external;

        internal WakeRequest(NetworkConfig network, WakeHostInfo host, bool external = false)
        {
            _network = network;
            _external = external;

            AddHost(host);
        }

        public bool IsExternal => this._external;
        public NetworkConfig Network => this._network;
        public IEnumerable<WakeHostInfo> Hosts => this._hosts;
        public WakeHostInfo RequestedHost => _hosts.Last();
        public WakeHostInfo TargetHost => _hosts.First();

        public string? FilteredBy { get; set; }
        public string? SendMethod { get; set; } = "Sent";

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
