using MadWizard.ARPergefactor.Wake.Filter.Rules;
using PacketDotNet;

namespace MadWizard.ARPergefactor.Wake.Filter
{
    internal class ServiceFilter(IEnumerable<ServiceFilterRule> rules) : IWakeFilter
    {
        public required WakeRequest Request { private get; init; }

        public bool NeedsIPUnicast => rules.Any();

        bool IWakeFilter.ShouldFilterPacket(EthernetPacket packet, out bool foundMatch)
        {
            foundMatch = false;

            if (packet.PayloadPacket is IPPacket ip && ip.PayloadPacket is TransportPacket transport)
            {
                foreach (var rule in rules)
                {
                    // should only apply for specific host?
                    if (rule.HostRule is HostFilterRule host)
                        if (!host.MatchesAddress(packet.SourceHardwareAddress, ip.SourceAddress))
                            continue; // ignore packet

                    if (rule.PayloadRules.Count == 0)
                    {
                        if (rule.Service.Matches(transport))
                            if (rule.Type == FilterRuleType.Must)
                            {
                                Request.Service = rule.Service;

                                foundMatch = true;
                            }
                            else
                                return true;
                    }
                    else if (rule.Service.Matches(transport))
                    {
                        foreach (var payload in rule.PayloadRules)
                        {
                            if (payload.Matches(transport.PayloadData))
                                if (payload.Type == FilterRuleType.Must)
                                {
                                    Request.Service = rule.Service;

                                    foundMatch = true;
                                }
                                else
                                    return true;
                        }
                    }
                }
            }

            return false;
        }
    }
}
