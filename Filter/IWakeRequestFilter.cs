using MadWizard.ARPergefactor.Request;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.ARPergefactor.Filter
{
    internal interface IWakeRequestFilter
    {
        Task<bool> FilterWakeRequest(WakeRequest request);
    }
}
