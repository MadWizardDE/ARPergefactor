using Autofac;
using Autofac.Core.Resolving.Pipeline;
using MadWizard.ARPergefactor.Neighborhood.Filter;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.ARPergefactor.Neighborhood.Discovery
{
    /// <summary>
    /// This middleware is responsible, that in the scope of the network, event NetworkHosts are treated as IEthernetListeners,
    /// even if they are not registered as such.
    /// </summary>
    internal class FilterPipeline : IResolveMiddleware
    {
        public PipelinePhase Phase => PipelinePhase.ServicePipelineEnd;

        public void Execute(ResolveRequestContext context, Action<ResolveRequestContext> next)
        {
            var network = context.ActivationScope.Resolve<Network>();

            next(context);

            var listeners = (context.Instance as IEnumerable<IEthernetListener>)!;

            context.Instance = network.Concat(listeners); // notify NetworkHost before WakeTriggers
        }
    }
}
