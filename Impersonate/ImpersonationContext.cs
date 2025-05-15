using Autofac.Core;
using Microsoft.Extensions.Hosting;
using PacketDotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MadWizard.ARPergefactor.Impersonate
{
    public class ImpersonationContext : IDisposable
    {
        private List<Impersonation>? _targets = [];

        internal void AddReferenceTo(Impersonation imp)
        {
            ObjectDisposedException.ThrowIf(_targets == null, this);

            _targets!.Add(imp += this);
        }

        public void Dispose()
        {
            if (_targets != null)
            {
                foreach (var target in _targets)
                    target.Release(this);

                _targets.Clear();
                _targets = null;

                Disposed?.Invoke(this, EventArgs.Empty);
            }
        }

        public event EventHandler? Disposed;
    }
}
