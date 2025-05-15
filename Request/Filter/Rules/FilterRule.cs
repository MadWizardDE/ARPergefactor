using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.ARPergefactor.Request.Filter.Rules
{
    internal abstract class FilterRule
    {
        public required FilterRuleType Type { get; set; }

        public virtual bool ShouldWhitelist => Type == FilterRuleType.Must;
        public virtual bool ShouldBlacklist => Type == FilterRuleType.MustNot;
    }

    internal enum FilterRuleType
    {
        Must,
        MustNot
    }
}
