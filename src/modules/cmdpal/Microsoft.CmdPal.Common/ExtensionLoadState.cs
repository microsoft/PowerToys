// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Common;

/// <summary>
/// Shared constants for the extension load sentinel file used by
/// <c>ProviderLoadGuard</c> and provider-specific crash sentinels to
/// coordinate crash detection across process lifetimes.
/// </summary>
public static class ExtensionLoadState
{
    /// <summary>
    /// Name of the sentinel JSON file written to the config directory.
    /// Both the app-level guard and individual extension sentinels must
    /// read and write the same file for crash detection to work.
    /// </summary>
    public const string SentinelFileName = "extensionLoadState.json";

    /// <summary>
    /// JSON property name storing the owning provider id for a guarded block.
    /// </summary>
    public const string ProviderIdKey = "providerId";

    /// <summary>
    /// JSON property name indicating a guarded block was active when the
    /// process exited.
    /// </summary>
    public const string LoadingKey = "loading";

    /// <summary>
    /// JSON property name storing the consecutive crash count for a guarded
    /// block.
    /// </summary>
    public const string CrashCountKey = "crashCount";

    /// <summary>
    /// Shared lock that must be held around every read-modify-write cycle
    /// on the sentinel file. Both <c>ProviderLoadGuard</c> and
    /// provider-specific crash sentinels run in the same process and would
    /// otherwise race on the file, silently dropping entries.
    /// </summary>
    public static readonly object SentinelFileLock = new();
}
