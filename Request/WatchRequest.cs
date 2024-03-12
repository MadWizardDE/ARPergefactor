using MadWizard.ARPergefactor.Config;
using PacketDotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Autofac.Core;

namespace MadWizard.ARPergefactor.Request
{
    internal class WatchRequest(HostInfo host) : Request
    {
        private List<Service> _services = [];

        public HostInfo Host => host;

        public WatchRequest AsResponseTo(Request request)
        {
            this.TriggerPacket = request.TriggerPacket;

            return this;
        }

        public void UntilService(ProtocolType type, uint? port)
        {
            _services.Add(new Service { Type = type, Port = port, Match = true });
        }

        public void UntilNotService(ProtocolType type, uint? port)
        {
            _services.Add(new Service { Type = type, Port = port, Match = false });
        }

        public bool Match(Packet packet)
        {
            foreach (Service service in _services)
                switch (service.Type)
                {
                    case ProtocolType.Tcp when packet.Extract<TcpPacket>() is TcpPacket tcp:
                        if (service.Port == null || service.Port == tcp.DestinationPort)
                            return service.Match;
                        break;
                }

            return false;
        }

        private struct Service
        {
            public ProtocolType Type;
            public uint? Port;

            public bool Match;
        }
    }
}
