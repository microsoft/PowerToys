// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using ManagedCommon;
using Microsoft.Windows.ApplicationModel.Resources;

namespace WorkspacesEditor
{
    internal static class ResourceLoaderInstance
    {
        private static readonly Lazy<ResourceLoader> _lazy = new(() =>
        {
            try
            {
                return new ResourceLoader("PowerToys.WorkspacesEditor.pri");
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to load ResourceLoader: " + ex.Message);
                return null;
            }
        });

        internal static ResourceLoader ResourceLoader => _lazy.Value;
    }
}
