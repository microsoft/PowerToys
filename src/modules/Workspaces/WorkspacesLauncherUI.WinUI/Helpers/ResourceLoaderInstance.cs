// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Windows.ApplicationModel.Resources;

namespace WorkspacesLauncherUI
{
    internal static class ResourceLoaderInstance
    {
        internal static ResourceLoader ResourceLoader { get; } = new ResourceLoader("PowerToys.WorkspacesLauncherUI.pri");
    }
}
