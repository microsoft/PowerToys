// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;

namespace PowerScripts.Core;

/// <summary>
/// The single runtime gate for whether the PowerScripts module is enabled in PowerToys.
///
/// Every surface — the Keyboard Manager hotkey, the Explorer context menu, and any future module —
/// runs a script through <c>PowerScripts.Host.exe run</c>. Enforcing the enabled state here (rather
/// than in each caller) means turning PowerScripts off makes *all* existing bindings inert without
/// having to find, delete, or rewrite them, and re-enabling restores them exactly as they were. Any
/// module that later integrates with PowerScripts inherits this behavior for free.
/// </summary>
public static class ModuleState
{
    /// <summary>
    /// Environment escape hatch (set to <c>1</c>) that bypasses the enabled gate, for hermetic tests
    /// and standalone/dev runs of the host outside a PowerToys installation.
    /// </summary>
    public const string IgnoreEnabledEnvironmentVariable = "POWERSCRIPTS_IGNORE_ENABLED";

    /// <summary>
    /// True when scripts are allowed to run.
    ///
    /// Resolution order:
    ///   - The <see cref="IgnoreEnabledEnvironmentVariable"/> bypass wins if set.
    ///   - If the PowerToys settings file is absent (no PowerToys governance — standalone/dev/test),
    ///     execution is allowed.
    ///   - If the settings file exists, the module must be explicitly enabled
    ///     (<c>enabled.PowerScripts == true</c>); an absent key means off, mirroring PowerToys'
    ///     "default off" for this module.
    ///   - If the settings file is unreadable, we fail open (allow) so a transient/corrupt file does
    ///     not silently break a user's existing hotkeys; the context-menu install/remove still tracks
    ///     the toggle in that edge case.
    /// </summary>
    public static bool IsPowerScriptsEnabled()
    {
        if (string.Equals(Environment.GetEnvironmentVariable(IgnoreEnabledEnvironmentVariable), "1", StringComparison.Ordinal))
        {
            return true;
        }

        return IsPowerScriptsEnabled(PowerScriptsPaths.PowerToysSettingsFilePath);
    }

    /// <summary>
    /// Core resolution against an explicit settings file (the production overload passes the
    /// well-known PowerToys path). Split out so the gate can be unit-tested hermetically, mirroring
    /// how <c>TrustStore</c> takes its file path.
    /// </summary>
    public static bool IsPowerScriptsEnabled(string settingsPath)
    {
        try
        {
            if (!File.Exists(settingsPath))
            {
                return true;
            }

            using var stream = File.OpenRead(settingsPath);
            using var document = JsonDocument.Parse(stream);
            if (document.RootElement.TryGetProperty("enabled", out var enabled) &&
                enabled.ValueKind == JsonValueKind.Object &&
                enabled.TryGetProperty("PowerScripts", out var flag))
            {
                return flag.ValueKind == JsonValueKind.True;
            }

            // Settings file present but no PowerScripts entry: the module is off by default.
            return false;
        }
        catch (Exception)
        {
            // Unreadable settings: cannot govern, so don't break existing bindings.
            return true;
        }
    }
}
