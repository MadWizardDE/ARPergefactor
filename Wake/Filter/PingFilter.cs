using MadWizard.ARPergefactor.Config;
using MadWizard.ARPergefactor.Impersonate;
using MadWizard.ARPergefactor.Wake.Filter.Rules;
using Microsoft.Extensions.Options;
using PacketDotNet;
using System.Net.Sockets;

namespace MadWizard.ARPergefactor.Wake.Filter
{
    internal class PingFilter(IEnumerable<PingFilterRule> rules) : IWakeFilter
    {
        public required WakeRequest Request { private get; init; }

        public bool NeedsIPUnicast => rules.Any();

        bool IWakeFilter.ShouldFilterPacket(EthernetPacket packet, out bool foundMatch)
        {
            foundMatch = false;

            // IPv4-Support
            if (packet.PayloadPacket is IPv4Packet ip4 && ip4.PayloadPacket is IcmpV4Packet icmp4)
            {
                foreach (var rule in rules)
                {
                    // should only apply for specific host?
                    if (rule.HostRule is HostFilterRule host)
                        if (!host.MatchesAddress(ip: ip4.SourceAddress))
                            continue; // ignore packet

                    if (icmp4.TypeCode == IcmpV4TypeCode.EchoRequest)
                        if (rule.Type == FilterRuleType.Must)
                            foundMatch = true;
                        else
                            return true;
                }
            }

            // IPv6-Support
            else if (packet.PayloadPacket is IPv6Packet ip6 && ip6.PayloadPacket is IcmpV6Packet icmp6)
            {
                foreach (var rule in rules)
                {
                    // should only apply for specific host?
                    if (rule.HostRule is HostFilterRule host)
                    {
                        if (!host.MatchesAddress(ip: ip6.SourceAddress))
                            continue; // ignore packet
                    }

                    if (icmp6.Type == IcmpV6Type.EchoRequest)
                        if (rule.Type == FilterRuleType.Must)
                            foundMatch = true;
                        else
                            return true;
                }
            }

            return false;
        }
    }
}
