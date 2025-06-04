using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.ARPergefactor.Wake.Filter.Rules
{
    public abstract class PayloadFilterRule : FilterRule
    {
        public abstract bool Matches(byte[] data);
    }
}
