using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.ARPergefactor.Neighborhood.Tables
{
    public class HostTable : DynamicTable<NetworkHost>
    {
        public NetworkHost? this[string name]
        {
            get => this.FirstOrDefault(h => h.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        public NetworkHost? this[IPAddress ip]
        {
            get => this.FirstOrDefault(h => h.HasAddress(ip: ip));
        }

        public NetworkHost? this[PhysicalAddress mac]
        {
            get => this.FirstOrDefault(h => h.HasAddress(mac: mac));
        }
    }
}
