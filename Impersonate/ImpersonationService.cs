using Autofac;
using Autofac.Features.OwnedInstances;
using MadWizard.ARPergefactor.Impersonate.Protocol;
using MadWizard.ARPergefactor.Neighborhood;
using MadWizard.ARPergefactor.Neighborhood.Filter;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PacketDotNet;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.ARPergefactor.Impersonate
{
    public class ImpersonationService : INetworkService
    {
        public required ILogger<ImpersonationService> Logger { private get; init; }

        public required ILifetimeScope Scope { private get; init; }

        public required Network Network { private get; init; }
        public required NetworkDevice Device { private get; init; }

        readonly HashSet<ImpersonationRequest> _requests = [];

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

            var owned = Scope.Resolve<Owned<T>>(
                new TypedParameter(typeof(IPAddress), ip), 
                new TypedParameter(typeof(PhysicalAddress), Device.PhysicalAddress));

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
                    _impersonations[address].Stop(silently);
                }
            }
        }

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
            foreach (var request in _requests.ToArray())
                request.Dispose();
        }

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
                _requests.Remove(request);

                SiftImpersonations(silently);
            }
        }
        #endregion
    }
}
