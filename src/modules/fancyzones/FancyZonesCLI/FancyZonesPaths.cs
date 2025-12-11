// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;

namespace FancyZonesCLI;

/// <summary>
/// Provides paths to FancyZones configuration files.
/// </summary>
internal static class FancyZonesPaths
{
    private static readonly string DataPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Microsoft",
        "PowerToys",
        "FancyZones");

    public static string AppliedLayouts => Path.Combine(DataPath, "applied-layouts.json");

    public static string CustomLayouts => Path.Combine(DataPath, "custom-layouts.json");

    public static string LayoutTemplates => Path.Combine(DataPath, "layout-templates.json");

    public static string LayoutHotkeys => Path.Combine(DataPath, "layout-hotkeys.json");

    public static string EditorParameters => Path.Combine(DataPath, "editor-parameters.json");
}
