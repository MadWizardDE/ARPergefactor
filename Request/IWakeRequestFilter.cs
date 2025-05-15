using PacketDotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.ARPergefactor.Request
{
    internal interface IWakeRequestFilter
    {
        /**
         * Filter the wake request.
         * 
         * @param request The request to filter.
         * @return true if the request should be filtered, false otherwise.
         */
        Task<bool?> ShouldFilterPacket(EthernetPacket packet);
    }
}
