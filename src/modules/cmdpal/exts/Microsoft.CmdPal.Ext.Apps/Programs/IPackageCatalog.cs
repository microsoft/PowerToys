// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.ApplicationModel;
using Windows.Foundation;

namespace Microsoft.CmdPal.Ext.Apps.Programs;

internal interface IPackageCatalog
{
    event TypedEventHandler<PackageCatalog, PackageInstallingEventArgs> PackageInstalling;

    event TypedEventHandler<PackageCatalog, PackageUninstallingEventArgs> PackageUninstalling;

    event TypedEventHandler<PackageCatalog, PackageUpdatingEventArgs> PackageUpdating;
}
