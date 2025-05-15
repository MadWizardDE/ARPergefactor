using MadWizard.ARPergefactor.Config;
using MadWizard.ARPergefactor.Request.Filter.Rules;
using Microsoft.Extensions.Options;
using PacketDotNet;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.ARPergefactor.Request.Filter
{
    internal class HostFilter(IEnumerable<HostFilterRule> rules) : IWakeRequestFilter
    {
        public required WakeRequest Request { private get; init; }

        async Task<bool?> IWakeRequestFilter.ShouldFilterPacket(EthernetPacket packet)
        {
            bool shouldFilter = rules.Any(rule => rule.ShouldWhitelist);

            foreach (var rule in rules)
            {
                if (rule.MatchesAddress(Request.SourcePhysicalAddress, Request.SourceIPAddress))
                    if (rule.Type == FilterRuleType.MustNot)
                        return true;
                    else
                        shouldFilter = false;
            }

            return shouldFilter;
        }
    }
}