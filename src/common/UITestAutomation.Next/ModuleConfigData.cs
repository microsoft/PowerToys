// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.UITest.Next;

/// <summary>
/// Modules of PowerToys that a <see cref="UITestBase"/> can target.
/// </summary>
/// <remarks>
/// <para>
/// <b>Launch model per scope</b> (see <see cref="SessionHelper.EnsureRunning"/>):
/// </para>
/// <list type="bullet">
///   <item><description><see cref="PowerToysSettings"/> — runner-owned. Launched via
///   <c>PowerToys.exe --open-settings</c> so the runner owns module toggles and activation hotkeys.
///   This is the scope to use when a test drives a utility <i>through the Settings UI</i>
///   (e.g. <c>ColorPicker.UITests</c>), because a standalone module exe has no runner behind it.</description></item>
///   <item><description><see cref="Runner"/> — launches <c>PowerToys.exe</c> directly (the tray/runner host).</description></item>
///   <item><description><b>Editor scopes</b> (<see cref="FancyZonesEditor"/>, <see cref="Hosts"/>,
///   <see cref="Workspaces"/>, <see cref="PowerRename"/>, <see cref="CommandPalette"/>,
///   <see cref="ScreenRuler"/>) — launch their own exe standalone. These are designed to run as
///   self-contained editor windows, so binding directly to the editor's window is correct.</description></item>
///   <item><description><see cref="ColorPicker"/>, <see cref="LightSwitch"/> — overlay/background
///   modules that are <i>not</i> meant to be launched standalone by a test; drive them through the
///   <see cref="PowerToysSettings"/> scope (toggle + activation hotkey) instead. The entries exist
///   so window/process discovery can still resolve them once the runner spawns them.</description></item>
/// </list>
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
/// Path resolution order: an explicit <c>POWERTOYS_INSTALL_DIR</c> override; then, when
/// <c>useInstallerForTest</c> is set, the installed build (Program Files / LocalAppData); otherwise
/// the build under test — located by walking up from the test assembly to the build-output root that
/// holds the exe (locally <c>&lt;root&gt;\&lt;plat&gt;\&lt;cfg&gt;</c>, in CI the downloaded build artifact) —
/// and finally the installed path as a last resort. This lets the same tests run against an installed
/// PowerToys or a dev / CI-artifact build without any environment configuration.
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

        // 1. Explicit override wins (CI can point at any layout).
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

        // 2. Installer mode forces the installed layout.
        if (EnvironmentConfig.UseInstallerForTest)
        {
            return installed;
        }

        // 3. Dev / CI-artifact mode: the build output that holds the exe is an ancestor of the test
        //    assembly. Prefer it so tests drive the build under test, not a stray machine install.
        if (TryComposeDevBuild(meta, out var dev))
        {
            return dev;
        }

        // 4. Last resort: an installed build if present (returns the installed path either way so a
        //    launch failure names a concrete location).
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

        // The build-output root that holds PowerToys.exe (and module subdirs like WinUI3Apps) is an
        // ancestor of the test assembly's bin folder — both locally
        // (<root>\<plat>\<cfg>\tests\<proj>\<tfm>\) and in CI (the downloaded build artifact, which
        // can nest <plat>\<cfg> more than once). Walk up and return the first ancestor that actually
        // contains the requested exe. Mirrors the legacy harness's "<assembly>\..\..\..\<exe>"
        // convention without hard-coding the depth.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Compose(dir.FullName, meta);
            if (File.Exists(candidate))
            {
                path = candidate;
                return true;
            }

            dir = dir.Parent;
        }

        // Fallback: repo root + conventional <plat>\<cfg> output, for the rare case the assembly
        // isn't located under the build tree.
        var root = RepoRoot.Value;
        if (!string.IsNullOrEmpty(root))
        {
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
