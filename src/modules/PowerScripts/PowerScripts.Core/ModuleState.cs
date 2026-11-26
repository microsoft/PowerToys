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
    /// Environment escape hatch (set to <c>1</c>) that bypasses the user enabled gate, for hermetic tests
    /// and standalone/dev runs of the host outside a PowerToys installation.
    /// </summary>
    public const string IgnoreEnabledEnvironmentVariable = "POWERSCRIPTS_IGNORE_ENABLED";

    /// <summary>
    /// Returns the policy-governed module state. Runner owns <c>enabled.PowerScripts</c> in general
    /// settings; a legacy module preference is used only when that flag has never been recorded.
    /// Missing preferences default off, and malformed or unreadable general settings fail closed.
    /// </summary>
    public static bool IsPowerScriptsEnabled()
    {
        var policy = PowerScriptsPolicies.ReadUtility("PowerScripts");
        if (policy.HasValue)
        {
            return policy.Value;
        }

        if (string.Equals(Environment.GetEnvironmentVariable(IgnoreEnabledEnvironmentVariable), "1", StringComparison.Ordinal))
        {
            return true;
        }

        return IsPowerScriptsEnabled(PowerScriptsPaths.PowerToysSettingsFilePath, PowerScriptsPaths.ReadEnabledOverride());
    }

    /// <summary>
    /// Core resolution against an explicit settings file (the production overload passes the
    /// well-known PowerToys path). Split out so the gate can be unit-tested hermetically, mirroring
    /// how <c>TrustStore</c> takes its file path.
    /// </summary>
    public static bool IsPowerScriptsEnabled(string settingsPath, bool? legacyEnabled = null)
    {
        try
        {
            using var stream = File.OpenRead(settingsPath);
            using var document = JsonDocument.Parse(stream);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            if (!document.RootElement.TryGetProperty("enabled", out var enabled))
            {
                return legacyEnabled ?? false;
            }

            if (enabled.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            return enabled.TryGetProperty("PowerScripts", out var flag)
                ? flag.ValueKind == JsonValueKind.True
                : legacyEnabled ?? false;
        }
        catch (FileNotFoundException)
        {
            return legacyEnabled ?? false;
        }
        catch (DirectoryNotFoundException)
        {
            return legacyEnabled ?? false;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
