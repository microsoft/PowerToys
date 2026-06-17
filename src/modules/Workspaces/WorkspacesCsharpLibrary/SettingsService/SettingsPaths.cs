// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Security.Principal;

namespace WorkspacesCsharpLibrary.SettingsService;

/// <summary>
/// Resolves the new (v6) and legacy paths used for the Workspaces data.
/// The new location lives under %ProgramData% in the service-managed
/// SettingsSvc tree, partitioned by namespace and per-user SID; only the
/// PTSettingsSvc service may write into it, but the owning user (and
/// Administrators) can read it directly.  The legacy location is the
/// pre-v6 %LocalAppData% file, used only by one-shot migration and the
/// no-service last-resort fallback.
/// </summary>
public static class SettingsPaths
{
    // Namespace id the Workspaces module is bound to in the service's
    // CallerBinding table (mirror of the native "Workspaces" namespace).
    private const string NamespaceId = "Workspaces";

    // %ProgramData%\Microsoft\PowerToys\SettingsSvc\<namespace>
    private const string SettingsSvcSubpath = @"Microsoft\PowerToys\SettingsSvc";

    // Pre-v6 per-user data folder under %LocalAppData%.
    private const string LegacySubpath = @"Microsoft\PowerToys\Workspaces";

    /// <summary>%ProgramData%\Microsoft\PowerToys\SettingsSvc\Workspaces</summary>
    public static string ServiceManagedNamespaceRoot()
    {
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        return Path.Combine(programData, SettingsSvcSubpath, NamespaceId);
    }

    /// <summary>%ProgramData%\Microsoft\PowerToys\SettingsSvc\Workspaces\&lt;current-user-sid&gt;</summary>
    public static string CurrentUserFolder()
    {
        var sid = WindowsIdentity.GetCurrent().User?.Value
                  ?? throw new InvalidOperationException("No current user SID");
        return Path.Combine(ServiceManagedNamespaceRoot(), sid);
    }

    /// <summary>The opaque per-user blob the service reads/writes (direct-read allowed).</summary>
    public static string CurrentUserBlobFile()
    {
        return Path.Combine(CurrentUserFolder(), "blob.bin");
    }

    /// <summary>The pre-v6 location.  Used by one-shot migration and the no-service fallback.</summary>
    public static string LegacyWorkspacesFile()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, LegacySubpath, "workspaces.json");
    }

    /// <summary>Sentinel dropped by the runner the first time a user is migrated.</summary>
    public static string MigrationSentinel()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, LegacySubpath, ".migrated-to-svc");
    }
}
