// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Windows.ApplicationModel.Resources;

namespace RobocopyUI.Helpers
{
    internal static class ResourceLoaderInstance
    {
        /// <summary>
        /// Gets the resource loader for the RobocopyUI module.
        /// </summary>
        internal static ResourceLoader ResourceLoader { get; private set; }

        static ResourceLoaderInstance()
        {
            ResourceLoader = new ResourceLoader("PowerToys.RobocopyUI.pri");
        }
    }
}
