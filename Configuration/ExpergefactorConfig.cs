﻿using MadWizard.ARPergefactor.Neighborhood;

namespace MadWizard.ARPergefactor.Config
{
    internal class ExpergefactorConfig
    {
        public required int Version { get; set; }

        public WatchScope? Scope { get; set; }

        public IList<NetworkConfig>? Network { get; set; }
    }
}
