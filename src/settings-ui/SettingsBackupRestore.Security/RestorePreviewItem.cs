// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.SettingsBackupRestore.Security;

/// <summary>
/// One testable row in a restore confirmation preview.
/// </summary>
public sealed record RestorePreviewItem(
    string Module,
    string SettingsPath,
    bool Included,
    string? ExclusionReason,
    RestoreMode RestoreMode);
