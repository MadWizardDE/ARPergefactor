using MadWizard.ARPergefactor.Neighborhood;
using MadWizard.ARPergefactor.Neighborhood.Filter;
using PacketDotNet;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.ARPergefactor.Impersonate
{
    internal abstract class Impersonation : /*IEthernetListener,*/ IDisposable
    {
        public required NetworkHost Host { protected get; init; }
        public required NetworkDevice Device { protected get; init; }

        public required IPAddress IPAddress { get; init; }

        private List<ImpersonationContext>? _references = [];

        public event EventHandler? PresenceDetected;
        public event EventHandler? Stopped;

        public static Impersonation operator +(Impersonation self, ImpersonationContext ctx)
        {
            ObjectDisposedException.ThrowIf(self._references == null, self);

            self._references.Add(ctx);

            return self;
        }

        protected void DetectPresence()
        {
            PresenceDetected?.Invoke(this, EventArgs.Empty);
        }

        public abstract bool Handle(EthernetPacket packet);

        internal void Release(ImpersonationContext context)
        {
            ObjectDisposedException.ThrowIf(_references == null, this);

            if (_references?.Remove(context) ?? false)
                if (_references.Count == 0)
                    Stop();
        }

        internal virtual void Stop(bool silently = false)
        {
            if (_references != null)
            {
                foreach (var context in _references.ToArray())
                {
                    _references.Remove(context);

                    context.Dispose();
                }

                _references = null;

                Stopped?.Invoke(this, EventArgs.Empty);
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }

    internal abstract class Impersonation<P> : Impersonation where P : Packet
    {
        internal abstract void StartWith(P? packet);
    }
}
