using PacketDotNet;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.ARPergefactor.Wake.Filter.Rules
{
    public class ServiceFilterRule(TransportService service) : FilterRule
    {
        public HostFilterRule? HostRule { get; set; }

        public List<PayloadFilterRule> PayloadRules { get; set; } = [];

        public TransportService Service => service;

        public override bool ShouldWhitelist
        {
            get
            {
                if (PayloadRules.Count > 0)
                    return PayloadRules.Any(rule => rule.ShouldWhitelist);
                else
                    return base.ShouldWhitelist;
            }
        }
    }

    public readonly struct TransportService(string name, TransportPortocolType protocolType, uint port)
    {
        public string Name { get; } = name;
        public TransportPortocolType ProtocolType { get; } = protocolType;
        public uint Port { get; } = port;

        public bool Matches(TransportPacket packet)
        {
            switch (packet)
            {
                case TcpPacket tcp when ProtocolType == TransportPortocolType.TCP:
                    return tcp.DestinationPort == Port;
                case UdpPacket udp when ProtocolType == TransportPortocolType.UDP:
                    return udp.DestinationPort == Port;
            }

            return false;
        }
    }

    public enum TransportPortocolType
    {
        TCP,
        UDP
    }
}
