// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.RegularExpressions;

namespace PowerScripts.Core.Execution;

/// <summary>
/// The data shapes a Python PowerScript can consume or produce. These map directly onto the
/// Advanced Paste clipboard formats so a Python PowerScript can be surfaced there, and onto the
/// file/text inputs used by the Keyboard Manager and context-menu surfaces.
/// </summary>
public enum PowerScriptDataFormat
{
    None,
    Text,
    Html,
    Image,
    Audio,
    Video,
    Files,
}

/// <summary>
/// The convention that turns a Python function name into a typed transform. Every Python PowerScript
/// declares exactly one function named <c>powerscript_from_&lt;input&gt;_to_&lt;output&gt;</c>; the
/// input/output tokens tell each surface what the script consumes and produces (e.g. which clipboard
/// formats it accepts in Advanced Paste). Keeping the contract in the function name means the same
/// <c>.py</c> file works unchanged whether it is invoked by a hotkey, the context menu, or a paste.
/// </summary>
public static partial class PowerScriptPythonConvention
{
    public const string FunctionPrefix = "powerscript_from_";

    [GeneratedRegex(@"^\s*def\s+(powerscript_from_(text|html|image|audio|video|files|none)_to_(text|html|image|audio|video|file|files|none))\s*\(", RegexOptions.Multiline)]
    private static partial Regex FunctionRegex();

    /// <summary>The parsed transform contract of a Python PowerScript.</summary>
    public sealed record TransformSignature(string FunctionName, PowerScriptDataFormat Input, PowerScriptDataFormat Output);

    /// <summary>
    /// Finds the single <c>powerscript_from_*_to_*</c> function declaration in the given Python
    /// source and returns its parsed input/output contract, or <c>null</c> when none (or more than
    /// one) is present.
    /// </summary>
    public static TransformSignature? Parse(string pythonSource)
    {
        if (string.IsNullOrEmpty(pythonSource))
        {
            return null;
        }

        var matches = FunctionRegex().Matches(pythonSource);
        if (matches.Count != 1)
        {
            return null;
        }

        var match = matches[0];
        return new TransformSignature(
            match.Groups[1].Value,
            ParseFormat(match.Groups[2].Value),
            ParseFormat(match.Groups[3].Value));
    }

    /// <summary>Reads and parses the transform signature from a Python file on disk.</summary>
    public static TransformSignature? ParseFile(string pythonFilePath)
    {
        try
        {
            return File.Exists(pythonFilePath) ? Parse(File.ReadAllText(pythonFilePath)) : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static PowerScriptDataFormat ParseFormat(string token) => token.ToLowerInvariant() switch
    {
        "text" => PowerScriptDataFormat.Text,
        "html" => PowerScriptDataFormat.Html,
        "image" => PowerScriptDataFormat.Image,
        "audio" => PowerScriptDataFormat.Audio,
        "video" => PowerScriptDataFormat.Video,
        "file" or "files" => PowerScriptDataFormat.Files,
        _ => PowerScriptDataFormat.None,
    };
}
