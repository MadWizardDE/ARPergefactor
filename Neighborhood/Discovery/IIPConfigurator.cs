namespace MadWizard.ARPergefactor.Neighborhood.Discovery
{
    internal interface IIPConfigurator
    {
        void ConfigureIPv4(NetworkHost host);
        void ConfigureIPv6(NetworkHost host);
    }
}
