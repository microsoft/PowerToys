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
    FancyZonesEditor,
    Hosts,
    Workspaces,
    PowerRename,
    CommandPalette,
    ScreenRuler,
    LightSwitch,
}

/// <summary>
/// Resolves executable paths, process names, and window titles for a <see cref="PowerToysModule"/>.
/// </summary>
/// <remarks>
/// Path resolution order: an explicit <c>POWERTOYS_INSTALL_DIR</c> override, then the installed
/// build (Program Files / LocalAppData), then the repo's dev-build output
/// (<c>&lt;root&gt;\&lt;plat&gt;\&lt;cfg&gt;</c>). Setting <c>useInstallerForTest</c> forces the installed
/// layout. This lets the same tests run against an installed PowerToys (CI) or a local dev build.
/// </remarks>
internal static class ModulePaths
{
    private sealed record ModuleMeta(string ExeName, string? SubDir, string ProcessName, string WindowTitle);

    private static readonly IReadOnlyDictionary<PowerToysModule, ModuleMeta> Meta =
        new Dictionary<PowerToysModule, ModuleMeta>
        {
            [PowerToysModule.PowerToysSettings] = new("PowerToys.Settings.exe", "WinUI3Apps", "PowerToys.Settings", "PowerToys Settings"),
            [PowerToysModule.Runner] = new("PowerToys.exe", null, "PowerToys", "PowerToys"),
            [PowerToysModule.ColorPicker] = new("PowerToys.ColorPickerUI.exe", null, "PowerToys.ColorPickerUI", "PowerToys.ColorPickerUI"),
            [PowerToysModule.FancyZonesEditor] = new("PowerToys.FancyZonesEditor.exe", null, "PowerToys.FancyZonesEditor", "FancyZones Layout"),
            [PowerToysModule.Hosts] = new("PowerToys.Hosts.exe", "WinUI3Apps", "PowerToys.Hosts", "Hosts File Editor"),
            [PowerToysModule.Workspaces] = new("PowerToys.WorkspacesEditor.exe", null, "PowerToys.WorkspacesEditor", "Workspaces Editor"),
            [PowerToysModule.PowerRename] = new("PowerToys.PowerRename.exe", "WinUI3Apps", "PowerToys.PowerRename", "PowerRename"),
            [PowerToysModule.CommandPalette] = new("Microsoft.CmdPal.UI.exe", "WinUI3Apps\\CmdPal", "Microsoft.CmdPal.UI", "PowerToys Command Palette"),
            [PowerToysModule.ScreenRuler] = new("PowerToys.MeasureToolUI.exe", "WinUI3Apps", "PowerToys.MeasureToolUI", "PowerToys.ScreenRuler"),
            [PowerToysModule.LightSwitch] = new("PowerToys.LightSwitch.exe", "LightSwitchService", "PowerToys.LightSwitch", "PowerToys.LightSwitch"),
        };

    private static readonly Lazy<string> InstalledRoot = new(ResolveInstalledRoot);
    private static readonly Lazy<string?> RepoRoot = new(FindRepoRoot);

    public static string ExePathFor(PowerToysModule module)
    {
        var meta = Meta[module];

        var overrideDir = Environment.GetEnvironmentVariable("POWERTOYS_INSTALL_DIR");
        if (!string.IsNullOrEmpty(overrideDir))
        {
            var overridePath = Compose(overrideDir, meta);
            if (File.Exists(overridePath))
            {
                return overridePath;
            }
        }

        var installed = Compose(InstalledRoot.Value, meta);

        if (EnvironmentConfig.UseInstallerForTest)
        {
            return installed;
        }

        if (File.Exists(installed))
        {
            return installed;
        }

        if (TryComposeDevBuild(meta, out var dev))
        {
            return dev;
        }

        return installed;
    }

    /// <summary>Process name as winappcli's <c>-a</c> flag accepts it (case-insensitive substring).</summary>
    public static string ProcessNameFor(PowerToysModule module) => Meta[module].ProcessName;

    /// <summary>Expected window title substring; used to pick the right HWND when a module has several windows.</summary>
    public static string MainWindowTitleFor(PowerToysModule module) => module switch
    {
        // The runner has no user-facing main window title to pin.
        PowerToysModule.Runner => string.Empty,
        _ => Meta[module].WindowTitle,
    };

    private static string Compose(string root, ModuleMeta meta) =>
        string.IsNullOrEmpty(meta.SubDir)
            ? Path.Combine(root, meta.ExeName)
            : Path.Combine(root, meta.SubDir, meta.ExeName);

    private static bool TryComposeDevBuild(ModuleMeta meta, out string path)
    {
        path = string.Empty;
        var root = RepoRoot.Value;
        if (string.IsNullOrEmpty(root))
        {
            return false;
        }

        foreach (var platform in new[] { "x64", "ARM64" })
        {
            foreach (var config in new[] { "Debug", "Release" })
            {
                var candidate = Compose(Path.Combine(root, platform, config), meta);
                if (File.Exists(candidate))
                {
                    path = candidate;
                    return true;
                }
            }
        }

        return false;
    }

    private static string ResolveInstalledRoot()
    {
        string[] candidates =
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "PowerToys"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "PowerToys"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PowerToys"),
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(Path.Combine(candidate, "PowerToys.exe")))
            {
                return candidate;
            }
        }

        return candidates[0];
    }

    private static string? FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "PowerToys.slnx")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        return null;
    }
}
