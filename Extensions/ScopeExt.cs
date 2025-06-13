using Autofac;
using MadWizard.ARPergefactor.Neighborhood.Filter;

namespace MadWizard.ARPergefactor.Extensions
{
    internal static class ScopeExt
    {
        public static TrafficShapeRequest UseTrafficShape(this ILifetimeScope scope, params ITrafficShape[] shapes)
        {
            return scope.Resolve<TrafficShapeRequest>(new TypedParameter(typeof(ITrafficShape[]), shapes));
        }
    }
}
