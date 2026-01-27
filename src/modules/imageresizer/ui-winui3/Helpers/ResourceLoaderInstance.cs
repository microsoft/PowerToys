// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Windows.ApplicationModel.Resources;

namespace ImageResizer.Helpers
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
                        _resourceLoader = new Microsoft.Windows.ApplicationModel.Resources.ResourceLoader("PowerToys.ImageResizer.pri", "PowerToys.ImageResizer/Resources");
                    }
                    catch
                    {
                        // Fallback: try with default resource map name
                        try
                        {
                            _resourceLoader = new Microsoft.Windows.ApplicationModel.Resources.ResourceLoader("PowerToys.ImageResizer.pri");
                        }
                        catch
                        {
                            // Last resort: use default resource loader
                            _resourceLoader = new Microsoft.Windows.ApplicationModel.Resources.ResourceLoader();
                        }
                    }
                }

                return _resourceLoader;
            }
        }
    }
}
