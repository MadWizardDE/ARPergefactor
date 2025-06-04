using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.ARPergefactor.Neighborhood.Tables
{
    public class DynamicTable<T> : IIEnumerable<T> where T : notnull
    {
        readonly HashSet<T> _staticEntries = [];
        readonly Dictionary<T, DateTime> _dynamicEntries = [];

        public event EventHandler<T>? Expired;

        public bool AddStaticEntry(T obj)
        {
            return _staticEntries.Add(obj);
        }

        public bool SetDynamicEntry(T obj, TimeSpan lifetime)
        {
            var expires = DateTime.Now + lifetime;

            if (_dynamicEntries.ContainsKey(obj))
            {
                _dynamicEntries[obj] = expires; // update lifetime

                return false; // entry was already present
            }

            _dynamicEntries.Add(obj, expires);

            return true; // entry was added
        }

        public bool RemoveEntry(T obj)
        {
            return _staticEntries.Remove(obj) || _dynamicEntries.Remove(obj);
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
                    _dynamicEntries.Remove(obj);

                    Expired?.Invoke(this, obj); // notify about expired entry
                }
            }
        }
    }
}
