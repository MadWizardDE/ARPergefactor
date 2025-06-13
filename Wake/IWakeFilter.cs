using PacketDotNet;

namespace MadWizard.ARPergefactor.Wake
{
    public interface IWakeFilter
    {
        bool NeedsIPUnicast { get; }

        /**
         * Filter the wake request.
         * 
         * @param request The request to filter.
         * @return true if the request should be filtered, false otherwise.
         */
        bool ShouldFilterPacket(EthernetPacket packet, out bool foundMatch);
    }

    internal class IPUnicastTrafficNeededException : Exception
    {

    }
}
