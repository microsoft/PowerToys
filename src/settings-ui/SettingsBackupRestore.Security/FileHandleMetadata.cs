// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.SettingsBackupRestore.Security;

/// <summary>
/// Metadata queried from the same handle used for file I/O.
/// </summary>
public readonly record struct FileHandleMetadata(bool IsDirectory, bool IsReparsePoint, uint ReparseTag, uint LinkCount, long Length);
