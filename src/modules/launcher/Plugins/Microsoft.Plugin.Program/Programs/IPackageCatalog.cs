using System;
using System.Collections.Generic;
using System.Text;
using Windows.ApplicationModel;
using Windows.Foundation;

namespace Microsoft.Plugin.Program.Programs
{
    internal interface IPackageCatalog
    {
        event TypedEventHandler<PackageCatalog, PackageInstallingEventArgs> PackageInstalling;
        event TypedEventHandler<PackageCatalog, PackageUninstallingEventArgs> PackageUninstalling;
        event TypedEventHandler<PackageCatalog, PackageUpdatingEventArgs> PackageUpdating;
    }
}
