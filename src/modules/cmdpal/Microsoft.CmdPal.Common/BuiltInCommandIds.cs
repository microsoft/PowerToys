// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Common;

/// <summary>
/// Stable command identifiers shared by built-in providers and host features that persist command references.
/// </summary>
/// <remarks>
/// These values are persisted by aliases, hotkeys, and pins. Changing a value requires migrating persisted references.
/// </remarks>
public static class BuiltInCommandIds
{
    private const string CmdPalPrefix = "com.microsoft.cmdpal.";

    public static readonly string Registry = BuildCmdPalId("registry");

    public static readonly string WindowsSettings = BuildCmdPalId("windowsSettings");

    public static readonly string Calculator = BuildCmdPalId("calculator");

    public static readonly string Run = BuildCmdPalId("run");

    public static readonly string WindowWalker = BuildCmdPalId("windowwalker");

    public static readonly string WebSearch = BuildCmdPalId("websearch");

    public static readonly string FileSearch = "com.microsoft.indexer.fileSearch";

    public static readonly string TimeDate = BuildCmdPalId("timedate");

    private static string BuildCmdPalId(string suffix) => CmdPalPrefix + suffix;
}
