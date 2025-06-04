using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.ARPergefactor.Reachability
{
    public class HostTimeoutException(TimeSpan timeout) : TimeoutException
    {
        public TimeSpan Timeout => timeout;
    }
}
