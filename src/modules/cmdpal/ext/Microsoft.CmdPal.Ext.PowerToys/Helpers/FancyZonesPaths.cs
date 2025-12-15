// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;

namespace PowerToysExtension.Helpers;

internal static class FancyZonesPaths
{
    private static readonly string DataRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Microsoft",
        "PowerToys",
        "FancyZones");

    public static string AppliedLayouts => Path.Combine(DataRoot, "applied-layouts.json");

    public static string CustomLayouts => Path.Combine(DataRoot, "custom-layouts.json");

    public static string LayoutTemplates => Path.Combine(DataRoot, "layout-templates.json");

    public static string EditorParameters => Path.Combine(DataRoot, "editor-parameters.json");
}
