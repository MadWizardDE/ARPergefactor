using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.ARPergefactor.Request.Filter.Rules
{
    internal abstract class PayloadFilterRule : FilterRule
    {
        public abstract bool Matches(byte[] data);
    }
}
