﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.ARPergefactor.Wake.Filter.Rules
{
    internal class PingFilterRule : FilterRule
    {
        public HostFilterRule? HostRule { get; set; }
    }
}
