// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

namespace WorkspacesCsharpLibrary.SettingsService;

/// <summary>
/// Inputs for <see cref="ServiceProvisioner.EnsureProvisioned"/>.  Paths are
/// supplied by the caller (resolved from the install folder) so the provisioner
/// stays free of host/registry dependencies and is fully testable.
/// </summary>
public sealed class ProvisionOptions
{
    /// <summary>Full path to the settings-service executable to register.</summary>
    public string? ServiceBinaryPath { get; init; }

    /// <summary>Full path to the per-user hardening script to run elevated.</summary>
    public string? HardenScriptPath { get; init; }

    /// <summary>SID of the user to harden; defaults to the current user when null/empty.</summary>
    public string? UserSid { get; init; }

    /// <summary>
    /// When true, bypass the "already attempted" back-off and prompt again.
    /// Use for explicit user actions (e.g. an "enable protection" toggle).
    /// </summary>
    public bool Force { get; init; }

    /// <summary>
    /// Optional override for how the elevated step is launched.  Defaults to
    /// <see cref="ServiceProvisioner.RunElevatedPowerShell"/> (a real UAC prompt).
    /// Tests and headless hosts can inject a direct runner.
    /// </summary>
    public ServiceProvisioner.ElevationRunner? ElevationRunner { get; init; }

    /// <summary>Builds options from a resolved PowerToys install folder.</summary>
    public static ProvisionOptions FromInstallFolder(string installFolder, bool force = false)
    {
        return new ProvisionOptions
        {
            ServiceBinaryPath = SettingsPaths.ServiceBinaryPath(installFolder),
            HardenScriptPath = SettingsPaths.HardenScriptPath(installFolder),
            Force = force,
        };
    }
}
