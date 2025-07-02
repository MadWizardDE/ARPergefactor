namespace MadWizard.ARPergefactor.Neighborhood.Discovery
{
    internal interface IIPDetector
    {
        void ConfigureIPv4(NetworkHost host);
        void ConfigureIPv6(NetworkHost host);
    }
}
