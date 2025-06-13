using System.Net;

namespace MadWizard.ARPergefactor.Impersonate
{
    public class ImpersonationRequest : IIEnumerable<IPAddress>, IDisposable
    {
        protected HashSet<IPAddress> Addresses { get; init; } = [];

        public event EventHandler? AddressesChanged;
        public event EventHandler<bool>? Disposed;

        private bool _disposed;

        public ImpersonationRequest(params IPAddress[] addresses)
        {
            foreach (var address in addresses)
            {
                Addresses.Add(address);
            }
        }

        public void AddAddress(IPAddress address)
        {
            if (Addresses.Add(address))
            {
                AddressesChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public bool Matches(IPAddress address)
        {
            return Addresses.Contains(address);
        }

        public void RemoveAddress(IPAddress address)
        {
            if (Addresses.Remove(address))
            {
                AddressesChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public void Dispose(bool silently = false)
        {
            if (!_disposed)
            {
                Disposed?.Invoke(this, silently);

                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(false);
        }

        public IEnumerator<IPAddress> GetEnumerator()
        {
            return Addresses.GetEnumerator();
        }
    }
}