// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;

namespace Microsoft.CmdPal.Ext.TextExpander.Snippets;

/// <summary>
/// Where PowerToys keeps the files this extension reads. It reads them only: Text Expander owns them.
/// </summary>
internal sealed class TextExpanderPaths
{
    /// <summary>Text Expander's key in PowerToys' "enabled" settings, and the name of its settings folder.</summary>
    public const string ModuleKey = "TextExpander";

    public TextExpanderPaths(string powerToysFolder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(powerToysFolder);
        PowerToysFolder = powerToysFolder;
    }

    public static TextExpanderPaths ForCurrentUser { get; } = new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Microsoft",
        "PowerToys"));

    public string PowerToysFolder { get; }

    /// <summary>Gets PowerToys' general settings, which say which utilities are on.</summary>
    public string GeneralSettingsFile => Path.Combine(PowerToysFolder, "settings.json");

    /// <summary>Gets Text Expander's folder: its settings, and the snippets file unless the user chose another.</summary>
    public string ModuleFolder => Path.Combine(PowerToysFolder, ModuleKey);

    public string ModuleSettingsFile => Path.Combine(ModuleFolder, "settings.json");
}
