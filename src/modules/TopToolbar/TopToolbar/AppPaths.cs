// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;

namespace TopToolbar;

internal static class AppPaths
{
    private const string RootFolderName = "TopToolbar";

    public static string Root => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), RootFolderName);

    public static string Logs => Path.Combine(Root, "Logs");

    public static string ConfigFile => Path.Combine(Root, "toolbar.config.json");

    public static string ProfilesDirectory => Path.Combine(Root, "Profiles");

    public static string ProvidersDirectory => Path.Combine(Root, "Providers");

    public static string ConfigDirectory => Path.Combine(Root, "config");

    public static string ProviderDefinitionsDirectory => Path.Combine(ConfigDirectory, "providers");

    public static string IconsDirectory => Path.Combine(Root, "icons");
}
