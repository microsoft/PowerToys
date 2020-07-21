using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;

namespace ColorPicker
{
    public static class Bootstrapper
    {
        public static CompositionContainer Container { get; private set; }

        public static void InitializeContainer(object initPoint)
        {
            var catalog = new AssemblyCatalog(System.Reflection.Assembly.GetExecutingAssembly());
            Container = new CompositionContainer(catalog);

            Container.SatisfyImportsOnce(initPoint);
        }

        public static void Dispose()
        {
            Container.Dispose();
        }
    }
}
