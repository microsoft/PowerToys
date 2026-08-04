// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Windows.ApplicationModel.Resources;

namespace FancyZonesEditor.Helpers
{
    internal static class ResourceLoaderInstance
    {
        internal static ResourceLoader ResourceLoader { get; private set; }

        static ResourceLoaderInstance()
        {
            ResourceLoader = new ResourceLoader("PowerToys.FancyZonesEditor.pri");
        }

        /// <summary>
        /// Convenience wrapper around <see cref="ResourceLoader.GetString(string)"/>.
        /// </summary>
        /// <param name="key">Resource key as declared in Strings/en-us/Resources.resw.</param>
        /// <returns>The localized string for <paramref name="key"/>.</returns>
        internal static string GetString(string key) => ResourceLoader.GetString(key);
    }
}
