// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Reflection;
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

    // Canonical file name kept inside the namespace folder (mirror of the
    // native CallerBinding fileName).  Keeps the original, human-readable name.
    private const string WorkspacesFileName = "workspaces.json";

    // %ProgramData%\Microsoft\PowerToys\Settings  (the service-managed store root)
    private const string SettingsStoreSubpath = @"Microsoft\PowerToys\Settings";

    // Pre-v6 per-user data folder under %LocalAppData%.
    private const string LegacySubpath = @"Microsoft\PowerToys\Workspaces";

    // Subfolder of the install root that carries the settings-service payload
    // (the service exe and the per-user hardening script).  The per-machine MSI
    // registers the service from here; the per-user install ships the same
    // payload unregistered so deferred initialization can register it lazily.
    private const string ServicePayloadSubdir = "WorkspacesSettingsService";

    /// <summary>File name of the settings-service executable.</summary>
    public const string ServiceBinaryName = "PowerToys.PTSettingsSvc.exe";

    /// <summary>File name of the signed service MSIX package (deferred install).</summary>
    public const string ServiceMsixName = "PTSettingsSvc.msix";

    /// <summary>%ProgramData%\Microsoft\PowerToys\Settings (the store root).</summary>
    public static string ServiceStoreRoot()
    {
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        return Path.Combine(programData, SettingsStoreSubpath);
    }

    /// <summary>%ProgramData%\Microsoft\PowerToys\Settings\&lt;current-user-sid&gt; (per-user node).</summary>
    public static string CurrentUserFolder()
    {
        var sid = WindowsIdentity.GetCurrent().User?.Value
                  ?? throw new InvalidOperationException("No current user SID");
        return Path.Combine(ServiceStoreRoot(), sid);
    }

    /// <summary>%ProgramData%\Microsoft\PowerToys\Settings\&lt;sid&gt;\Workspaces (namespace folder).</summary>
    public static string CurrentUserNamespaceFolder()
    {
        return Path.Combine(CurrentUserFolder(), NamespaceId);
    }

    /// <summary>The per-user settings file the service reads/writes (direct-read allowed).</summary>
    public static string CurrentUserFile()
    {
        return Path.Combine(CurrentUserNamespaceFolder(), WorkspacesFileName);
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

    /// <summary>
    /// Sentinel recording that deferred service provisioning has already been
    /// attempted for this user AT THIS VERSION, so repeated trigger points don't
    /// re-prompt for elevation — but a later upgrade (new version) legitimately
    /// re-prompts once to re-point the service to the new binary (upgrade
    /// path).  Lives under %LocalAppData% (user-writable): it only governs UX
    /// back-off, never security.
    /// </summary>
    public static string ProvisionAttemptSentinel()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0";
        return Path.Combine(localAppData, LegacySubpath, ".svc-provision-attempted-" + version);
    }

    /// <summary>Folder under the install root that carries the settings-service payload.</summary>
    public static string ServicePayloadDir(string installFolder)
    {
        ArgumentException.ThrowIfNullOrEmpty(installFolder);
        return Path.Combine(installFolder, ServicePayloadSubdir);
    }

    /// <summary>Full path to the settings-service executable inside an install folder.</summary>
    public static string ServiceBinaryPath(string installFolder)
    {
        return Path.Combine(ServicePayloadDir(installFolder), ServiceBinaryName);
    }

    /// <summary>Full path to the signed service MSIX package inside an install folder.</summary>
    public static string ServiceMsixPath(string installFolder)
    {
        return Path.Combine(ServicePayloadDir(installFolder), ServiceMsixName);
    }
}
