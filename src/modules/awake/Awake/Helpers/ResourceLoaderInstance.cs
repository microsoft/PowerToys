// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using Windows.ApplicationModel.Resources;

namespace Awake.Helpers
{
    internal static class ResourceLoaderInstance
    {
        internal static ResourceLoader ResourceLoader { get; private set; }

        static ResourceLoaderInstance()
        {
            ResourceLoader = new ResourceLoader("PowerToys.Awake.pri");
        }
    }
}
