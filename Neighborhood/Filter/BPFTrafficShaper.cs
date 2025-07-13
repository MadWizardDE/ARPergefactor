namespace MadWizard.ARPergefactor.Neighborhood.Filter
{
    #region Traffic Shapes
    public interface ITrafficShape;

    public readonly record struct ARPTrafficShape : ITrafficShape;
    public readonly record struct NDPTrafficShape : ITrafficShape;
    public readonly record struct WOLTrafficShape : ITrafficShape;

    public readonly record struct IPv4TrafficShape : ITrafficShape;
    public readonly record struct IPv6TrafficShape : ITrafficShape;

    public readonly record struct TCPTrafficShape(uint Port) : ITrafficShape;
    public readonly record struct UDPTrafficShape(uint Port) : ITrafficShape;

    public readonly record struct ICMPEchoTrafficShape : ITrafficShape;
    #endregion

    public class BPFTrafficShaper(NetworkDevice device)
    {
        readonly HashSet<TrafficShapeRequest> _requests = [];

        private IEnumerable<ITrafficShape> Shapes => _requests.SelectMany(x => x.Shapes).Distinct();

        internal void AddRequest(TrafficShapeRequest request)
        {
            if (_requests.Add(request))
            {
                request.Disposed += (sender, args) =>
                {
                    _requests.Remove(request);

                    UpdateFilter();
                };

                UpdateFilter();
            }
        }

        private void UpdateFilter()
        {
            string ip4TCP = "ip and (not tcp or (tcp[tcpflags] & tcp-syn != 0))";
            string ip6TCP = "ip6 and (ip6[6] != 6 or (ip6[40] & 0x02 != 0))"; // BPF cannot use symbols for any protocol higher than IPv6

            if (!Shapes.OfType<UDPTrafficShape>().Any())
            {
                ip4TCP += " and not udp";
                ip6TCP += " and ip6[6] != 17";
            }

            string filter = $"(not ip and not ip6) or (({ip4TCP}) or ({ip6TCP}))";

            // TODO consider other shapes

            device.Filter = filter;
        }
    }

    public class TrafficShapeRequest : IDisposable
    {
        public ITrafficShape[] Shapes { get; private init; }

        public event EventHandler? Disposed;

        private bool _disposed;

        public TrafficShapeRequest(BPFTrafficShaper filter, ITrafficShape[] shapes)
        {
            Shapes = shapes;

            filter.AddRequest(this);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                Disposed?.Invoke(this, EventArgs.Empty);

                _disposed = true;
            }
        }
    }
}
