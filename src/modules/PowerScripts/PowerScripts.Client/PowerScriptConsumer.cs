// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerScripts.Client;

/// <summary>Stable, caller-supplied attribution, not an authenticated security identity.</summary>
public static class PowerScriptConsumer
{
    public const string AdvancedPaste = "advanced-paste";
    public const string KeyboardManager = "keyboard-manager";
    public const string CommandLine = "command-line";

    public static bool IsValid(string? id) =>
        id is { Length: > 0 and <= 64 } &&
        id[0] is >= 'a' and <= 'z' &&
        id.All(character => character is >= 'a' and <= 'z' or >= '0' and <= '9' or '-');
}
