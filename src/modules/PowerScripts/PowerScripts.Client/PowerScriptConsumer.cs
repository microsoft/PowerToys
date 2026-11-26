// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerScripts.Client;

/// <summary>Stable, caller-supplied attribution, not an authenticated security identity.</summary>
public static class PowerScriptConsumer
{
    /// <summary>Attribution for Advanced Paste.</summary>
    public const string AdvancedPaste = "advanced-paste";

    /// <summary>Attribution for Keyboard Manager.</summary>
    public const string KeyboardManager = "keyboard-manager";

    /// <summary>Attribution for Command Palette.</summary>
    public const string CommandPalette = "command-palette";

    /// <summary>Attribution for command-line invocations.</summary>
    public const string CommandLine = "command-line";

    /// <summary>Attribution for the PowerScripts editor.</summary>
    public const string Editor = "powerscripts-editor";

    /// <summary>Checks whether a consumer ID has the supported bounded syntax.</summary>
    public static bool IsValid(string? id) =>
        id is { Length: > 0 and <= 64 } &&
        id[0] is >= 'a' and <= 'z' &&
        id.All(character => character is >= 'a' and <= 'z' or >= '0' and <= '9' or '-');
}
