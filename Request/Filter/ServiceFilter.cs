using MadWizard.ARPergefactor.Config;
using MadWizard.ARPergefactor.Impersonate;
using MadWizard.ARPergefactor.Request.Filter.Rules;
using Microsoft.Extensions.Options;
using PacketDotNet;
using System.Net.Sockets;

namespace MadWizard.ARPergefactor.Request.Filter
{
    internal class ServiceFilter(IEnumerable<ServiceFilterRule> rules) : IWakeRequestFilter
    {
        public required WakeRequest Request { private get; init; }

        async Task<bool?> IWakeRequestFilter.ShouldFilterPacket(EthernetPacket packet)
        {
            bool shouldFilter = rules.Any(rule => rule.ShouldWhitelist);

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

                                shouldFilter = false;
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

                                    shouldFilter = false;
                                }
                                else
                                    return true;
                        }
                    }
                }
            }
            else if (rules.Any())
            {
                return null;
            }

            return shouldFilter;
        }
    }
}
