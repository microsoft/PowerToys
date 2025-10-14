// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Ext.Bookmarks.Helpers;

public sealed record Classification(
    CommandKind Kind,
    string Input,
    string Target,
    string Arguments,
    LaunchMethod Launch,
    string? WorkingDirectory,
    bool IsPlaceholder,
    string? FileSystemTarget = null,
    string? DisplayName = null)
{
    public static Classification Unknown(string rawInput) =>
        new(CommandKind.Unknown, rawInput, rawInput, string.Empty, LaunchMethod.ShellExecute, string.Empty, false, null, null);
}
