// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.SettingsBackupRestore.Security;

/// <summary>
/// A normalized entry from a validated PowerToys backup archive.
/// </summary>
public sealed record ArchiveEntryDescriptor(string RelativePath, bool IsDirectory, long UncompressedLength);
