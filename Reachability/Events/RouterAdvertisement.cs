﻿using System.Net;
using System.Net.NetworkInformation;

namespace MadWizard.ARPergefactor.Reachability.Events
{
    public class RouterAdvertisement(PhysicalAddress mac, IPAddress ip, TimeSpan lifetime) : AddressAdvertisement(ip, lifetime)
    {
        public PhysicalAddress PhysicalAddress => mac;
    }
}
