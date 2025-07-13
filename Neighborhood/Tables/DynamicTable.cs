using ConcurrentCollections;
using System.Collections.Concurrent;

namespace MadWizard.ARPergefactor.Neighborhood.Tables
{
    public class DynamicTable<T> : IIEnumerable<T> where T : notnull
    {
        readonly ConcurrentHashSet<T> _staticEntries = [];
        readonly ConcurrentDictionary<T, DateTime> _dynamicEntries = [];

        public event EventHandler<T>? Expired;

        public bool AddStaticEntry(T obj)
        {
            return _staticEntries.Add(obj);
        }

        public bool SetDynamicEntry(T obj, TimeSpan lifetime)
        {
            if (_staticEntries.Contains(obj))
                return false;

            var expires = DateTime.Now + lifetime;

            if (_dynamicEntries.ContainsKey(obj))
            {
                _dynamicEntries[obj] = expires; // update lifetime

                return false; // entry was already present
            }

            _dynamicEntries[obj] = expires;

            return true; // entry was added
        }

        public bool RemoveEntry(T obj)
        {
            return _staticEntries.TryRemove(obj) || _dynamicEntries.TryRemove(obj, out _);
        }

        public IEnumerator<T> GetEnumerator()
        {
            foreach (var obj in _staticEntries)
            {
                yield return obj;
            }

            List<T>? remove = null;
            foreach (var entry in _dynamicEntries)
            {
                if (entry.Value > DateTime.Now)
                {
                    yield return entry.Key;
                }
                else
                {
                    (remove ??= []).Add(entry.Key);
                }
            }

            if (remove != null)
            {
                foreach (var obj in remove)
                {
                    _dynamicEntries.TryRemove(obj, out _);

                    Expired?.Invoke(this, obj); // notify about expired entry
                }
            }
        }
    }
}
