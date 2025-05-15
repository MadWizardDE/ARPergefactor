using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.ARPergefactor.Request.Filter.Rules
{
    internal class PingFilterRule : FilterRule
    {
        public HostFilterRule? HostRule { get; set; }
    }
}
