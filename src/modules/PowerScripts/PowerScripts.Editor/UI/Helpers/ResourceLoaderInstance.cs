// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Resources;

namespace PowerScripts.UI.Helpers;

public sealed class ResourceLoaderInstance
{
    private readonly ResourceManager _resources = new(
        "PowerScripts.UI.Strings.Resources",
        typeof(ResourceLoaderInstance).Assembly);

    private ResourceLoaderInstance()
    {
    }

    public static ResourceLoaderInstance ResourceLoader { get; } = new();

    public string GetString(string name) =>
        _resources.GetString(name, CultureInfo.CurrentUICulture)
        ?? throw new MissingManifestResourceException($"Missing PowerScripts resource: {name}");
}
