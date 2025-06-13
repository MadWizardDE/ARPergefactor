using MadWizard.ARPergefactor.Neighborhood;
using MadWizard.ARPergefactor.Reachability.Events;
using System.Collections.Concurrent;
using System.Net;

namespace MadWizard.ARPergefactor.Reachability
{
    public class ReachabilityTest : IIEnumerable<IPAddress>, IDisposable
    {
        readonly SemaphoreSlim _semaphore = new(0);

        protected readonly ConcurrentDictionary<IPAddress, bool> _reachableByIP = [];

        readonly TimeSpan _timeout;

        public ReachabilityTest(IEnumerable<IPAddress> addresses, TimeSpan timeout)
        {
            _timeout = timeout;

            foreach (var address in addresses)
            {
                _reachableByIP[address] = false;
            }
        }

        public bool this[IPAddress ip] => _reachableByIP[ip];

        public void NotifyReachable(IPAddress address)
        {
            if (_reachableByIP.TryGetValue(address, out bool reachable) && reachable == false)
            {
                _reachableByIP[address] = true;

                _semaphore.Release();
            }
        }

        public async Task<bool> RespondedTimely()
        {
            return await _semaphore.WaitAsync(_timeout);
        }

        IEnumerator<IPAddress> IEnumerable<IPAddress>.GetEnumerator() => _reachableByIP.Keys.GetEnumerator();

        public virtual void Dispose()
        {
            _semaphore.Dispose();
        }
    }

    public class HostReachabilityTest : ReachabilityTest
    {
        readonly NetworkWatchHost _host;

        public HostReachabilityTest(NetworkWatchHost host, TimeSpan? timeout = null) : base(host.IPAddresses, timeout ?? host.PingMethod.Timeout)
        {
            _host = host;
            _host.AddressAdded += Host_AddressAdded;
            _host.AddressRemoved += Host_AddressRemoved;
        }

        private void Host_AddressAdded(object? sender, AddressEventArgs args) => _reachableByIP[args.IPAddress] = false;
        private void Host_AddressRemoved(object? sender, AddressEventArgs args) => _reachableByIP.Remove(args.IPAddress, out _);

        public override void Dispose()
        {
            _host?.AddressAdded -= Host_AddressAdded;
            _host?.AddressRemoved -= Host_AddressRemoved;

            base.Dispose();
        }
    }
}