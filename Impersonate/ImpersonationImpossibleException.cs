using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.ARPergefactor.Impersonate
{
    internal class ImpersonationImpossibleException : Exception
    {
        public ImpersonationImpossibleException(string? message) : base(message)
        {

        }
    }
}
