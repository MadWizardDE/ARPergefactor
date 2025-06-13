using Autofac;
using MadWizard.ARPergefactor.Extensions;
using MadWizard.ARPergefactor.Neighborhood.Filter;

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
