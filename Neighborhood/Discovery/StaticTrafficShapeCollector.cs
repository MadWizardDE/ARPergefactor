using Autofac;
using MadWizard.ARPergefactor.Extensions;
using MadWizard.ARPergefactor.Neighborhood.Filter;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.ARPergefactor.Neighborhood.Discovery
{
    internal class StaticTrafficShapeCollector
    {
        readonly HashSet<ITrafficShape> shapes = [];

        public void PushTo(ILifetimeScope scope)
        {
            scope.UseTrafficShape([.. shapes]);

            shapes.Clear();
        }

        public static StaticTrafficShapeCollector operator +(StaticTrafficShapeCollector bag, ITrafficShape shape)
        {
            bag.shapes.Add(shape);

            return bag;
        }
    }
}
