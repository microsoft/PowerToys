// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using Microsoft.Windows.ApplicationModel.Resources;

namespace PowerOCR.Helpers;

internal static class ResourceLoaderInstance
{
    private static readonly Lazy<ResourceLoader> Loader =
        new(() => new ResourceLoader("PowerToys.PowerOCR.pri"));

    internal static ResourceLoader ResourceLoader => Loader.Value;
}
