﻿using Autofac;
using MadWizard.ARPergefactor.Impersonate;
using MadWizard.ARPergefactor.Impersonate.Methods;
using MadWizard.ARPergefactor.Reachability.Methods;
using MadWizard.ARPergefactor.Wake;
using MadWizard.ARPergefactor.Wake.Methods;
using PacketDotNet;

namespace MadWizard.ARPergefactor.Neighborhood
{
    public class NetworkWatchHost(string name) : NetworkHost(name)
    {
        public required ILifetimeScope Scope { private get; init; }

        public PingMethod PingMethod { get; set; }
        public PoseMethod PoseMethod { get; set; }
        public WakeMethod WakeMethod { get; set; }

        public DateTime? LastSeen { get; internal set { field = value; Seen?.Invoke(this, EventArgs.Empty); } }
        public DateTime? LastUnseen { get; internal set { field = value; Unseen?.Invoke(this, EventArgs.Empty); } }
        public DateTime? LastWake { get; internal set { field = value; Wake?.Invoke(this, EventArgs.Empty); } }

        public event EventHandler<EventArgs>? Seen;
        public event EventHandler<EventArgs>? Unseen;
        public event EventHandler<EventArgs>? Wake;

        public (ILifetimeScope, WakeRequest) StartWakeRequest(EthernetPacket trigger)
        {
            ILifetimeScope scope = Scope.BeginLifetimeScope(MatchingScopeLifetimeTags.RequestLifetimeScopeTag);

            Scope.Disposer.AddInstanceForDisposal(scope);

            return (scope, scope.Resolve<WakeRequest>(TypedParameter.From(trigger)));
        }

        public ImpersonationRequest Impersonate(EthernetPacket trigger)
        {
            if (Scope.ResolveOptional<Imposter>() is Imposter imposter)
            {
                return imposter.Impersonate(trigger);
            }

            throw new ImpersonationImpossibleException("Imposter is unavailable.");
        }
    }

    public class VirtualWatchHost(string name) : NetworkWatchHost(name)
    {
        public required NetworkWatchHost PhysicalHost { get; init; }

        public WakeOnLANRedirection Rediretion { get; set; } = WakeOnLANRedirection.OnlyIfNotFiltered;
    }
}
