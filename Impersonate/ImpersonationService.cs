using Autofac;
using Autofac.Features.OwnedInstances;
using ConcurrentCollections;
using MadWizard.ARPergefactor.Impersonate.Protocol;
using MadWizard.ARPergefactor.Neighborhood;
using MadWizard.ARPergefactor.Neighborhood.Filter;
using Microsoft.Extensions.Logging;
using PacketDotNet;
using System.Collections.Concurrent;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace MadWizard.ARPergefactor.Impersonate
{
    public class ImpersonationService : INetworkService
    {
        public required ILogger<ImpersonationService> Logger { private get; init; }

        public required ILifetimeScope Scope { private get; init; }

        public required Network Network { private get; init; }
        public required NetworkDevice Device { private get; init; }

        readonly ConcurrentHashSet<ImpersonationRequest> _requests = [];

        readonly ConcurrentDictionary<IPAddress, Impersonation> _impersonations = [];

        public bool IsImpersonating(IPAddress ip)
        {
            if (_impersonations.IsEmpty)
                return false;

            return _impersonations.ContainsKey(ip);
        }

        public void Impersonate(ImpersonationRequest request, bool advertise = false)
        {
            foreach (var address in request)
            {
                if (!IsImpersonating(address))
                {
                    Impersonation imp = address.AddressFamily switch
                    {
                        AddressFamily.InterNetwork      => StartImpersonation<ARPImpersonation>(address),
                        AddressFamily.InterNetworkV6    => StartImpersonation<NDPImpersonation>(address),

                        _ => throw new NotImplementedException()
                    };

                    if (advertise && Network.Options.WatchScope == WatchScope.Network)
                    {
                        imp.SendAdvertisement();
                    }
                }
            }

            if (_requests.Add(request))
            {
                request.AddressesChanged += ImpersonationRequest_Changed;
                request.Disposed += ImpersonationRequest_Disposed;
            }

            SiftImpersonations();
        }

        private T StartImpersonation<T>(IPAddress ip) where T : Impersonation
        {
            Logger.LogDebug($"Starting impersonation of IP {ip}");

            var mac = Device.PhysicalAddress;

            var owned = Scope.Resolve<Owned<T>>(
                new TypedParameter(typeof(IPAddress), ip), 
                new TypedParameter(typeof(PhysicalAddress), mac));

            (_impersonations[ip] = owned.Value).Stopped += (sender, args) =>
            {
                owned.Dispose();

                _impersonations.Remove(ip, out _);
            };

            return owned.Value;
        }

        private void SiftImpersonations(bool silently = false)
        {
            foreach (var address in _impersonations.Keys.ToArray())
            {
                if (!_requests.Any(request => request.Matches(address))) // still used?
                {
                    if (_impersonations.TryGetValue(address, out var imp))
                    {
                        imp.Stop(silently);
                    }
                }
            }
        }

        #region NetworkService callbacks
        void INetworkService.ProcessPacket(EthernetPacket packet)
        {
            if (Network.IsInScope(packet))
            {
                foreach (var imp in _impersonations.Values)
                {
                    imp.ProcessPacket(packet);
                }
            }
        }

        void INetworkService.Shutdown()
        {
            Logger.LogDebug($"Shutting down... (Remaining requests: {_requests.Count})");

            foreach (var request in _requests.ToArray())
            {
                request.Dispose();
            }
        }
        #endregion

        #region ImpersonationRequest events
        private void ImpersonationRequest_Changed(object? sender, EventArgs args)
        {
            if (sender is ImpersonationRequest request)
            {
                Impersonate(request);
            }
        }
        private void ImpersonationRequest_Disposed(object? sender, bool silently)
        {
            if (sender is ImpersonationRequest request)
            {
                if (_requests.TryRemove(request))
                {
                    SiftImpersonations(silently);
                }
            }
        }
        #endregion
    }
}
