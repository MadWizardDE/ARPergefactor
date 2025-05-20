using System.Net.NetworkInformation;
using System.Net;
using PacketDotNet;
using MadWizard.ARPergefactor.Neighborhood;
using System.Threading.Channels;
using MadWizard.ARPergefactor.Impersonate;
using MadWizard.ARPergefactor.Logging;
using MadWizard.ARPergefactor.Request.Filter.Rules;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Diagnostics;

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

        public required ILogger<WakeRequest> Logger { private get; init; }
        public IEnumerable<IWakeRequestFilter> Filters { private get; set; } = [];
        public IEnumerable<FilterRule> Rules { private get; set; } = [];

        public required WakeLogger WakeLogger { private get; init; }

        public required EthernetPacket TriggerPacket { get; set; }
        public PhysicalAddress? SourcePhysicalAddress => TriggerPacket?.FindSourcePhysicalAddress();
        public IPAddress? SourceIPAddress => TriggerPacket?.FindSourceIPAddress();

        public TransportService? Service { get; set; }

        private Channel<EthernetPacket> IncomingQueue { get => field ??= Channel.CreateUnbounded<EthernetPacket>(); } = null!;
        private Queue<EthernetPacket> OutgoingQueue { get => field ??= new Queue<EthernetPacket>(); } = null!;

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

        public void EnqueuePacket(EthernetPacket packet, bool outgoing = false)
        {
            if (outgoing)
            {
                OutgoingQueue.Enqueue(packet);
            }
            else
            {
                IncomingQueue.Writer.TryWrite(packet);
            }
        }

        private async Task<EthernetPacket?> DequeuePacket(CancellationToken token)
        {
            try
            {
                var packet = await IncomingQueue.Reader.ReadAsync(token);

                return packet;
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        }

        public bool Verify(EthernetPacket packet)
        {
            bool needIPUnicast = false;
            bool needMatch = Rules.Any(rule => rule.ShouldWhitelist);

            foreach (var filter in Filters)
            {
                needIPUnicast = filter.NeedsIPUnicast || needIPUnicast;

                if (filter.ShouldFilterPacket(packet, out bool foundMatch))
                {
                    _ = WakeLogger.LogFilteredRequest(this, filter);

                    return false;
                }
                else
                {
                    if (foundMatch)
                    { 
                        needMatch = false; // no need to find a match anymore
                    }
                }
            }

            if (needIPUnicast && !packet.IsIPUnicast())
            {
                /*
                 * If one filter could not decide if it likes the packet or not,
                 * we must check if the packet originated from this node
                 * or if we are already redirecting the traffic to us.
                 * 
                 * If neither is the case, we have to impersonate to
                 * receive more packets, to finally make our decision.
                 */
                if (packet.FindDestinationIPAddress() is not IPAddress ip || !Network.IsImpersonating(ip))
                {
                    throw new IPUnicastTrafficNeededException();
                }
            }

            return !needMatch;
        }

        public int ForwardPackets()
        {
            Logger.LogTrace($"FORWARD {OutgoingQueue.Count} packet(s) of {this}");

            foreach (EthernetPacket packet in OutgoingQueue)
            {
                packet.SourceHardwareAddress = Device.PhysicalAddress;
                packet.DestinationHardwareAddress = Host.PhysicalAddress;


                Device.SendPacket(packet);
            }

            OutgoingQueue.Clear();

            return OutgoingQueue.Count;
        }

        public async Task<bool> CheckReachability()
        {
            if (!Host.HasBeenSeen())
            {
                Logger.LogTrace($"Checking reachability of host '{Host.Name}'...");

                var watch = Stopwatch.StartNew();

                try
                {
                    TimeSpan latency;
                    if (TriggerPacket.FindDestinationIPAddress() is IPAddress ip)
                    {
                        switch (ip.AddressFamily)
                        {
                            case AddressFamily.InterNetwork:
                                latency = await Host.DoARPing(ip);
                                break;

                            case AddressFamily.InterNetworkV6:
                                latency = await Host.DoNDPing(ip);
                                break;

                            default:
                                throw new Exception($"Unsupported address family {ip.AddressFamily} for {Host.Name}");
                        }
                    }
                    else
                    {
                        latency = await Host.SendICMPEchoRequest();
                    }

                    Logger.LogDebug($"Received response from '{Host.Name}' after {Math.Ceiling(latency.TotalMilliseconds)} ms");

                    return true;
                }
                catch (TimeoutException)
                {
                    Logger.LogDebug($"Received NO response from '{Host.Name}' after {watch.ElapsedMilliseconds} ms");

                    return false; // host is most probably offline
                }
            }

            Logger.LogTrace($"Received last response from '{Host.Name}' since {(DateTime.Now - Host.LastSeen)?.TotalMilliseconds} ms");

            return true; // host was seen lately
        }

        public override string ToString()
        {
            return $"WakeRequest#{nr} for '{Host.Name}'";
        }
    }
}
