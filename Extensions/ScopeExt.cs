using Autofac;
using MadWizard.ARPergefactor.Neighborhood.Filter;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
