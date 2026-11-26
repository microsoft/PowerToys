// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerScripts.Client;

/// <summary>Creation-time guidance only; consumers still validate actual scripts and inputs at execution.</summary>
public static class PowerScriptConsumerGuidance
{
    /// <summary>Checks the primary contract accepted by a consumer's current adapter.</summary>
    public static bool Supports(string consumer, string input, string output) => consumer switch
    {
        PowerScriptConsumer.KeyboardManager => input == "none" && IsShape(output),
        PowerScriptConsumer.CommandPalette => input == "none" && output is "none" or "text",
        PowerScriptConsumer.LightSwitch => input == "none" && output == "none",
        PowerScriptConsumer.AdvancedPaste => input is "text" or "files" or "image" or "audio" &&
            output is "text" or "files" or "image" or "audio",
        "file-explorer" => input is "files" or "image" or "audio" && IsShape(output),
        _ => false,
    };

    /// <summary>Checks a contract against the intersection of the selected consumers.</summary>
    public static bool Allows(IEnumerable<string> consumers, string input, string output) =>
        IsShape(input) && IsShape(output) && consumers.All(consumer => Supports(consumer, input, output));

    /// <summary>Checks whether at least one primary contract is shared.</summary>
    public static bool CanCombine(IEnumerable<string> consumers) =>
        Shapes.Any(input => Shapes.Any(output => Allows(consumers, input, output)));

    private static IReadOnlyList<string> Shapes { get; } = ["none", "text", "files", "image", "audio"];

    private static bool IsShape(string shape) => Shapes.Contains(shape, StringComparer.Ordinal);
}
