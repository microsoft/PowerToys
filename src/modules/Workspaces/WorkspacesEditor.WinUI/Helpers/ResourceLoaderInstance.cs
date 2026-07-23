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
        private static ResourceLoader _resourceLoader;

        internal static ResourceLoader ResourceLoader
        {
            get
            {
                if (_resourceLoader == null)
                {
                    try
                    {
                        _resourceLoader = new ResourceLoader("PowerToys.WorkspacesEditor.pri");
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("Failed to load ResourceLoader: " + ex.Message);
                    }
                }

                return _resourceLoader;
            }
        }
    }
}
