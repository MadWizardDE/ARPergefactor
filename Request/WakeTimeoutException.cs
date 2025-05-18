using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.ARPergefactor.Request
{
    internal class WakeTimeoutException(TimeSpan timeout) : TimeoutException
    {
        public TimeSpan Timeout => timeout;
    }
}
