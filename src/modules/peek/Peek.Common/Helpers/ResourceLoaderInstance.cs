// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using Microsoft.Windows.ApplicationModel.Resources;

namespace Peek.Common.Helpers
{
    public static class ResourceLoaderInstance
    {
        public static ResourceLoader ResourceLoader { get; private set; }

        static ResourceLoaderInstance()
        {
            ResourceLoader = new Microsoft.Windows.ApplicationModel.Resources.ResourceLoader("PowerToys.Peek.UI.pri");
        }
    }
}
