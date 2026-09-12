// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.SettingsBackupRestore.Security;

/// <summary>
/// Restore behavior for one settings file.
/// </summary>
public enum RestoreMode
{
    /// <summary>
    /// Create a settings file that does not currently exist.
    /// </summary>
    Create,

    /// <summary>
    /// Merge backup JSON into the current JSON.
    /// </summary>
    Merge,

    /// <summary>
    /// Replace current JSON with backup JSON.
    /// </summary>
    Overwrite,
}
