// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.PowerToys.SettingsBackupRestore.Security;

/// <summary>
/// UI-independent shape for restore confirmation.
/// </summary>
public sealed class RestorePreviewViewModel
{
    /// <summary>
    /// Explains why confirmation cannot replace secure archive and filesystem enforcement.
    /// </summary>
    public const string SecurityBoundaryNotice =
        "Confirmation records user intent; validated archive names and handle-relative I/O remain the security boundary.";

    private RestorePreviewViewModel(IReadOnlyList<RestorePreviewItem> items, bool restartAfterRestore, string? archiveFileName, string? archiveSha256)
    {
        Items = items;
        RestartAfterRestore = restartAfterRestore;
        ArchiveFileName = archiveFileName;
        ArchiveSha256 = archiveSha256;
    }

    /// <summary>
    /// Gets settings and exclusions displayed before restore.
    /// </summary>
    public IReadOnlyList<RestorePreviewItem> Items { get; }

    /// <summary>
    /// Gets whether PowerToys will restart after a successful restore.
    /// </summary>
    public bool RestartAfterRestore { get; }

    /// <summary>
    /// Gets the archive selected for this preview.
    /// </summary>
    public string? ArchiveFileName { get; }

    /// <summary>
    /// Gets the selected archive content identity.
    /// </summary>
    public string? ArchiveSha256 { get; }

    /// <summary>
    /// Gets the explicit security-boundary statement for the confirmation surface.
    /// </summary>
    public string SecurityBoundaryStatement => SecurityBoundaryNotice;

    /// <summary>
    /// Builds a deterministic preview from archive and current settings paths.
    /// </summary>
    public static RestorePreviewViewModel Create(
        BackupRestorePolicy policy,
        IEnumerable<string> archiveSettingsPaths,
        IEnumerable<string> currentSettingsPaths)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(archiveSettingsPaths);
        ArgumentNullException.ThrowIfNull(currentSettingsPaths);

        SortedSet<string> candidates = new(WindowsPathComparer.Instance);
        foreach (string path in archiveSettingsPaths)
        {
            if (!string.IsNullOrWhiteSpace(path))
            {
                candidates.Add(SecurePath.NormalizeRelative(path.TrimStart('\\', '/')));
            }
        }

        SortedSet<string> currentPaths = new(WindowsPathComparer.Instance);
        foreach (string path in currentSettingsPaths)
        {
            if (!string.IsNullOrWhiteSpace(path))
            {
                currentPaths.Add(SecurePath.NormalizeRelative(path.TrimStart('\\', '/')));
            }
        }

        List<RestorePreviewItem> items = [];
        foreach (string path in candidates)
        {
            if (string.Equals(path, "manifest.json", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            bool ignored = policy.IsIgnored(path);
            bool included = policy.ShouldInclude(path);
            string? exclusionReason = included ? null : ignored ? "Excluded by IgnoreFiles" : "Not matched by IncludeFiles";
            string module = GetModule(path);
            RestoreMode restoreMode = currentPaths.Contains(path) ? policy.GetRestoreMode(path) : RestoreMode.Create;
            items.Add(new RestorePreviewItem(module, path, included, exclusionReason, restoreMode));
        }

        return new RestorePreviewViewModel(items, policy.RestartAfterRestore, archiveFileName: null, archiveSha256: null);
    }

    internal RestorePreviewViewModel WithArchiveIdentity(string archiveFileName, string archiveSha256)
    {
        return new RestorePreviewViewModel(Items, RestartAfterRestore, archiveFileName, archiveSha256);
    }

    private static string GetModule(string path)
    {
        string firstComponent = path.Split(Path.DirectorySeparatorChar, 2)[0];
        return string.Equals(firstComponent, "settings.json", StringComparison.OrdinalIgnoreCase) ? "General" : firstComponent;
    }
}
