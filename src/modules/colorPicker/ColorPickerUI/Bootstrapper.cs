// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("UnitTest-ColorPickerUI")]

namespace ColorPicker
{
    public static class Bootstrapper
    {
        public static CompositionContainer Container { get; private set; }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "This is properly disposed of in MainWindow.Xaml.cs")]
        public static void InitializeContainer(object initPoint)
        {
            var catalog = new AssemblyCatalog(System.Reflection.Assembly.GetExecutingAssembly());
            Container = new CompositionContainer(catalog);

            Container.SatisfyImportsOnce(initPoint);
        }

        public static void Dispose()
            => Container.Dispose();
    }
}
