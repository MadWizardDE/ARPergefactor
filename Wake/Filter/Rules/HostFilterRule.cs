using MadWizard.ARPergefactor.Neighborhood;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.ARPergefactor.Wake.Filter.Rules
{
    public abstract class HostFilterRule : FilterRule
    {
        public abstract bool MatchesAddress(PhysicalAddress? mac = null, IPAddress? ip = null);
    }

    public class StaticHostFilterRule : HostFilterRule
    {
        public PhysicalAddress? PhysicalAddress { get; set; }

        public List<IPAddress> IPAddresses { get; set; } = [];

        public override bool MatchesAddress(PhysicalAddress? mac = null, IPAddress? ip = null)
        {
            if (mac != null && PhysicalAddress != null)
                if (!mac.Equals(PhysicalAddress))
                    return false;

            if (ip != null && IPAddresses.Count > 0)
                if (!IPAddresses.Contains(ip))
                    return false;

            return true;
        }
    }

    public class DynamicHostFilterRule(NetworkHost host) : HostFilterRule
    {
        public override bool MatchesAddress(PhysicalAddress? mac = null, IPAddress? ip = null)
        {
            if (host.PhysicalAddress != null)
                if (mac != null && !host.HasAddress(mac:mac))
                    return false;

            if (host.IPAddresses.Any())
                if (ip != null && !host.HasAddress(ip:ip))
                    return false;

            return true;
        }
    }
}
