// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

namespace WorkspacesCsharpLibrary.SettingsService;

/// <summary>
/// Inputs for <see cref="SettingsBootstrapper.EnsureInitialized"/>.  Hosts build
/// this from their own context (install-path resolver, optional test seam).
/// </summary>
public sealed class BootstrapRequest
{
    /// <summary>What triggered the bootstrap (an explicit request bypasses back-off).</summary>
    public SettingsBootstrapper.TriggerReason Reason { get; init; }

    /// <summary>
    /// Resolved PowerToys install folder.  When null/empty, service provisioning
    /// is skipped and only migration (with its no-service fallback) runs.
    /// </summary>
    public string? InstallFolder { get; init; }

    /// <summary>Optional elevation override forwarded to the provisioner (tests / headless).</summary>
    public ServiceProvisioner.ElevationRunner? ElevationRunner { get; init; }
}
