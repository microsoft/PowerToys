// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.UITest.Next;

/// <summary>
/// Modules of PowerToys that a <see cref="UITestBase"/> can target.
/// </summary>
public enum PowerToysModule
{
    PowerToysSettings,
    Runner,
    ColorPicker,
}

/// <summary>Resolves installer paths and process metadata for a <see cref="PowerToysModule"/>.</summary>
internal static class ModulePaths
{
    private static readonly string Root = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
        "PowerToys");

    public static string ExePathFor(PowerToysModule module) => module switch
    {
        PowerToysModule.PowerToysSettings => Path.Combine(Root, "WinUI3Apps", "PowerToys.Settings.exe"),
        PowerToysModule.Runner => Path.Combine(Root, "PowerToys.exe"),
        PowerToysModule.ColorPicker => Path.Combine(Root, "PowerToys.ColorPickerUI.exe"),
        _ => throw new ArgumentOutOfRangeException(nameof(module), module, null),
    };

    /// <summary>Process name as winappcli's <c>-a</c> flag accepts it (case-insensitive substring).</summary>
    public static string ProcessNameFor(PowerToysModule module) => module switch
    {
        PowerToysModule.PowerToysSettings => "PowerToys.Settings",
        PowerToysModule.Runner => "PowerToys",
        PowerToysModule.ColorPicker => "PowerToys.ColorPickerUI",
        _ => throw new ArgumentOutOfRangeException(nameof(module), module, null),
    };

    /// <summary>Expected window title substring; used to pick the right HWND when a module has several windows.</summary>
    public static string MainWindowTitleFor(PowerToysModule module) => module switch
    {
        PowerToysModule.PowerToysSettings => "PowerToys Settings",
        PowerToysModule.ColorPicker => "PowerToys.ColorPickerUI",
        _ => string.Empty,
    };
}
