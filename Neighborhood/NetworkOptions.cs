namespace MadWizard.ARPergefactor.Neighborhood
{
    public struct NetworkOptions
    {
        public NetworkOptions()
        {

        }

        public WatchScope? WatchScope { get; set; }

        public uint? WatchUDPPort { get; set; }
    }

    public enum WatchScope
    {
        Network,
        Host,
    }
}
