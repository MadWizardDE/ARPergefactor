using System.Net.NetworkInformation;
using System.Net;
using PacketDotNet;
using MadWizard.ARPergefactor.Neighborhood;
using System.Threading.Channels;
using MadWizard.ARPergefactor.Impersonate;
using MadWizard.ARPergefactor.Logging;
using MadWizard.ARPergefactor.Request.Filter.Rules;
using System.Net.Sockets;

namespace MadWizard.ARPergefactor.Request
{
    public class WakeRequest
    {
        private static int NR = 1;

        readonly int nr = NR++;

        public required Network Network { get; init; }
        public required NetworkHost Host { get; init; }
        public required NetworkDevice Device { private get; init; }

        public required Imposter Imposter { private get; init; }

        public IEnumerable<IWakeRequestFilter> Filters { private get; set; } = [];

        public required WakeLogger WakeLogger { private get; init; }

        public required EthernetPacket TriggerPacket { get; init; }
        public PhysicalAddress? SourcePhysicalAddress => TriggerPacket?.FindSourcePhysicalAddress();
        public IPAddress? SourceIPAddress => TriggerPacket?.FindSourceIPAddress();

        public TransportService? Service { get; set; }

        private Channel<EthernetPacket> PacketQueue { get => field ??= Channel.CreateUnbounded<EthernetPacket>(); } = null!;

        public ImpersonationContext Impersonate()
        {
            return Imposter.Impersonate(TriggerPacket);
        }

        public async IAsyncEnumerable<EthernetPacket> ReadPackets(TimeSpan timeout)
        {
            var cts = new CancellationTokenSource(timeout);

            while (await DequeuePacket(cts.Token) is EthernetPacket packet)
            {
                yield return packet;
            }
        }

        public void EnqueuePacket(EthernetPacket packet)
        {
            PacketQueue.Writer.TryWrite(packet);
        }

        private async Task<EthernetPacket?> DequeuePacket(CancellationToken token)
        {
            try
            {
                return await PacketQueue.Reader.ReadAsync(token);
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        }

        public async Task<bool> Verify(EthernetPacket packet)
        {
            bool missingData = false;
            foreach (var filter in Filters)
            {
                switch (await filter.ShouldFilterPacket(packet))
                {
                    case true:
                        await WakeLogger.LogFilteredRequest(this, filter);
                        return false;

                    case false:
                        continue;

                    case null:
                        missingData = true; 
                        continue;
                }
            }

            if (missingData)
            {
                /*
                 * If one filter could not decide if it likes the packet or not,
                 * we must check if the packet originated from this node
                 * or if we are already redirecting the traffic to us.
                 * 
                 * If neither is the case, we have to impersonate to
                 * receive more packets, to finally make our decision.
                 */
                // TODO check if trigger is discovery?
                if (packet.FindDestinationIPAddress() is not IPAddress ip || !Network.IsImpersonating(ip))
                {
                    throw new UnicastTrafficNeededException();
                }

                return false;
            }

            return true;
        }

        public override string ToString()
        {
            return $"WakeRequest#{nr} for '{Host.Name}'";
        }
    }
}
