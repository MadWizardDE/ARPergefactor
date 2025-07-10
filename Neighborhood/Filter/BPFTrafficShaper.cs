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
            const string ip4TCP = "ip and (not tcp or (tcp[tcpflags] & tcp-syn != 0))";
            const string ip6TCP = "ip6 and (not tcp or (ip6[40] & 0x02 != 0))";

            string filter = $"(not ip and not ip6) or (({ip4TCP}) or ({ip6TCP}))";

            if (!Shapes.OfType<UDPTrafficShape>().Any())
            {
                filter += " and not udp";
            }

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
