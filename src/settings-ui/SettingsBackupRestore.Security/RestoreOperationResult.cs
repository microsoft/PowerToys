// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.SettingsBackupRestore.Security;

/// <summary>
/// Result of a production restore.
/// </summary>
public sealed record RestoreOperationResult(bool SettingsChanged, bool RestartRequired);
