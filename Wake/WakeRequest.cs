﻿using MadWizard.ARPergefactor.Impersonate;
using MadWizard.ARPergefactor.Neighborhood;
using MadWizard.ARPergefactor.Wake.Filter.Rules;
using Microsoft.Extensions.Logging;
using PacketDotNet;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading.Channels;

namespace MadWizard.ARPergefactor.Wake
{
    public class WakeRequest
    {
        public required ILogger<WakeRequest> Logger { private get; init; }

        public int Number { get; internal set; }

        public required Network Network { get; init; }
        public required NetworkDevice Device { private get; init; }
        public required NetworkWatchHost Host { get; init; }

        public bool SkipFilters { get; set; } = false;
        public IEnumerable<IWakeFilter> Filters { private get; set; } = [];
        public IEnumerable<FilterRule> Rules { private get; set; } = [];

        public required EthernetPacket TriggerPacket { get; set; }
        public PhysicalAddress? SourcePhysicalAddress => TriggerPacket?.FindSourcePhysicalAddress();
        public IPAddress? SourceIPAddress => TriggerPacket?.FindSourceIPAddress();

        public TransportService? Service { get; set; }

        private Channel<EthernetPacket> IncomingQueue { get => field ??= Channel.CreateUnbounded<EthernetPacket>(); } = null!;
        private Queue<EthernetPacket> OutgoingQueue { get => field ??= new Queue<EthernetPacket>(); } = null!;

        public ImpersonationRequest? Impersonate()
        {
            try
            {
                return Host.Impersonate(TriggerPacket);
            }
            catch (ImpersonationImpossibleException)
            {
                if (Network.Options.WatchScope == WatchScope.Host)
                    return null; // we don't event want this

                throw;
            }
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
            if (SkipFilters)
            {
                Logger.LogTrace($"Skipping filters...");

                return true;
            }

            bool needIPUnicast = false;
            bool needMatch = Rules.Any(rule => rule.ShouldWhitelist);

            foreach (var filter in Filters)
            {
                needIPUnicast = filter.NeedsIPUnicast || needIPUnicast;

                if (filter.ShouldFilterPacket(packet, out bool foundMatch))
                {
                    _ = Logger.LogFilteredRequest(this, filter);

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
                if (Network.Options.WatchScope == WatchScope.Network)
                {
                    packet.SourceHardwareAddress = Device.PhysicalAddress;
                    packet.DestinationHardwareAddress = Host.PhysicalAddress;
                }

                Device.SendPacket(packet);
            }

            OutgoingQueue.Clear();

            return OutgoingQueue.Count;
        }

        public override string ToString()
        {
            return $"WakeRequest#{Number}";
        }
    }
}
