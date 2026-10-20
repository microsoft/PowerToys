// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerScripts.Core.Storage;

/// <summary>
/// The single place the module resolves its <see cref="ISettingsStore"/>. Every consumer of the
/// security-bearing settings (the trust store and <c>config.json</c>) goes through
/// <see cref="Current"/>, which makes this the one hook point for switching to the protected,
/// tamper-resistant per-user settings store when the local settings service lands.
///
/// Today <see cref="Current"/> is always the file-backed store rooted at the module data directory,
/// so all current functionality — including pointing PowerScripts at a folder of your choosing —
/// behaves exactly as before. To move the trust store and config into the protected store later, this
/// is the only code that changes: return a broker-backed <see cref="ISettingsStore"/> (bound to the
/// <see cref="Namespace"/>) here, guarded by a capability/flag check. Callers stay untouched.
/// </summary>
public static class SettingsStore
{
    /// <summary>
    /// The namespace the protected-settings broker will bind PowerScripts blobs to (each module gets
    /// its own namespace so a caller can only reach its own settings). Unused by the file-backed store
    /// but declared now so the future broker-backed implementation has a stable name to bind to.
    /// </summary>
    public const string Namespace = "PowerScripts";

    private static ISettingsStore? _current;

    /// <summary>
    /// The active settings store. Assignable so a future protected-store implementation (or tests) can
    /// inject an alternative; defaults to the file-backed store when never set.
    /// </summary>
    public static ISettingsStore Current
    {
        get => _current ??= CreateDefault();
        set => _current = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>Resets to the default file-backed store (primarily for tests).</summary>
    public static void ResetToDefault() => _current = CreateDefault();

    private static ISettingsStore CreateDefault()
    {
        // Current phase: file-backed, under %LOCALAPPDATA%\Microsoft\PowerToys\PowerScripts. Keeps the
        // exact files (trust.json, config.json) the module has always used.
        //
        // Future (protected settings service): select a broker-backed ISettingsStore here — e.g.
        //   if (ProtectedSettingsService.IsAvailable) return new ProtectedSettingsStore(Namespace);
        // The blob keys stay the same; only the transport (named pipe to the admin-owned store)
        // changes, so the trust store and config become read-only to a non-elevated user and can only
        // be mutated through the service. No caller of ISettingsStore needs to change.
        return new FileSettingsStore(PowerScriptsPaths.ModuleDirectory);
    }
}
