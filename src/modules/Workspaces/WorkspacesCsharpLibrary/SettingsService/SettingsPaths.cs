// Copyright (c) Microsoft Corporation
// Licensed under the MIT license.

using System;
using System.IO;
using System.Security.Principal;

namespace WorkspacesCsharpLibrary.SettingsService;

/// <summary>
/// Resolves the new (v6) and legacy paths used for the Workspaces data
/// file.  The new location lives under %ProgramData% with a per-user SID
/// subfolder; only the PTWorkspacesSvc service may write into it, but the
/// owning user (and Administrators) can read it directly, which keeps the
/// launcher's hot path free of any IPC.
/// </summary>
public static class SettingsPaths
{
    private const string PtSubpath = @"Microsoft\PowerToys\Workspaces";

    /// <summary>%ProgramData%\Microsoft\PowerToys\Workspaces</summary>
    public static string ServiceManagedRoot()
    {
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        return Path.Combine(programData, PtSubpath);
    }

    /// <summary>%ProgramData%\Microsoft\PowerToys\Workspaces\&lt;current-user-sid&gt;</summary>
    public static string CurrentUserFolder()
    {
        var sid = WindowsIdentity.GetCurrent().User?.Value
                  ?? throw new InvalidOperationException("No current user SID");
        return Path.Combine(ServiceManagedRoot(), sid);
    }

    /// <summary>Per-user workspaces.json under the service-managed root.</summary>
    public static string CurrentUserWorkspacesFile()
        => Path.Combine(CurrentUserFolder(), "workspaces.json");

    /// <summary>Per-user temp draft used by the Editor → Launcher hand-off.</summary>
    public static string CurrentUserTempWorkspacesFile()
        => Path.Combine(CurrentUserFolder(), "temp-workspaces.json");

    /// <summary>The pre-v6 location.  Used only by the one-shot migration.</summary>
    public static string LegacyWorkspacesFile()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, PtSubpath, "workspaces.json");
    }

    /// <summary>Sentinel dropped by the service the first time a user is migrated.</summary>
    public static string MigrationSentinel()
        => Path.Combine(CurrentUserFolder(), ".migrated");
}
