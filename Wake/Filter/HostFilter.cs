using MadWizard.ARPergefactor.Config;
using MadWizard.ARPergefactor.Wake.Filter.Rules;
using Microsoft.Extensions.Options;
using PacketDotNet;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.ARPergefactor.Wake.Filter
{
    internal class HostFilter(IEnumerable<HostFilterRule> rules) : IWakeFilter
    {
        public required WakeRequest Request { private get; init; }

        public bool NeedsIPUnicast => false;

        bool IWakeFilter.ShouldFilterPacket(EthernetPacket packet, out bool foundMatch)
        {
            foundMatch = false;

            foreach (var rule in rules)
            {
                if (rule.MatchesAddress(Request.SourcePhysicalAddress, Request.SourceIPAddress))
                    if (rule.Type == FilterRuleType.MustNot)
                        return true;
                    else
                        foundMatch = true;
            }

            return false;
        }
    }
}