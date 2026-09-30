// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using PowerScripts.Core.Execution;

namespace PowerScripts.Core.Manifest;

/// <summary>
/// Resolves the input/output data shapes of a PowerScript. This replaces the old author-declared
/// "surfaces" list: a script says only what it consumes and produces, and each consuming module
/// discovers the scripts it can use by filtering on that I/O (e.g. Advanced Paste asks for scripts
/// that accept <c>Text</c> when text is on the clipboard). Because scripts no longer name the modules
/// that host them, adding a new consuming module never requires editing a single script.
///
/// <para>
/// The I/O is explicit when the author declares it (<see cref="PowerScriptManifest.InputFormat"/> /
/// <see cref="PowerScriptManifest.OutputFormat"/>), and otherwise inferred:
/// </para>
/// <list type="bullet">
///   <item><description>A Python <c>powerscript_from_&lt;in&gt;_to_&lt;out&gt;</c> function gives both shapes directly.</description></item>
///   <item><description>A script with a declared file <see cref="PowerScriptManifest.Input"/> consumes <c>Files</c>; it produces <c>Files</c> when it converts, else nothing.</description></item>
///   <item><description>Otherwise the script consumes nothing and produces text (its stdout).</description></item>
/// </list>
///
/// <para>
/// The old <c>system</c>/<c>file</c> "kind" concept is fully derived from this I/O and no longer
/// declared or stored: a script is "file-driven" precisely when its resolved input shape is
/// <see cref="PowerScriptDataFormat.Files"/>.
/// </para>
/// </summary>
public static class ScriptIo
{
    /// <summary>The resolved (input, output) data shapes for a script.</summary>
    public static (PowerScriptDataFormat Input, PowerScriptDataFormat Output) Resolve(
        PowerScriptManifest manifest,
        PowerScriptPythonConvention.TransformSignature? signature)
    {
        PowerScriptDataFormat inferredInput;
        PowerScriptDataFormat inferredOutput;

        if (signature is not null)
        {
            inferredInput = signature.Input;
            inferredOutput = signature.Output;
        }
        else if (manifest.Input is not null)
        {
            inferredInput = PowerScriptDataFormat.Files;
            inferredOutput = manifest.Output?.Type == ScriptOutputType.ConvertedFile
                ? PowerScriptDataFormat.Files
                : PowerScriptDataFormat.None;
        }
        else
        {
            inferredInput = PowerScriptDataFormat.None;
            inferredOutput = PowerScriptDataFormat.Text;
        }

        return (manifest.InputFormat ?? inferredInput, manifest.OutputFormat ?? inferredOutput);
    }

    /// <summary>The lowercase wire token for a data shape (e.g. <c>text</c>, <c>files</c>, <c>none</c>).</summary>
    public static string ToToken(PowerScriptDataFormat format) => format.ToString().ToLowerInvariant();

    /// <summary>Parses a wire/author token into a data shape, or null when unrecognized/empty.</summary>
    public static PowerScriptDataFormat? TryParse(string? token) => token?.Trim().ToLowerInvariant() switch
    {
        null or "" => null,
        "none" => PowerScriptDataFormat.None,
        "text" => PowerScriptDataFormat.Text,
        "html" => PowerScriptDataFormat.Html,
        "image" => PowerScriptDataFormat.Image,
        "audio" => PowerScriptDataFormat.Audio,
        "video" => PowerScriptDataFormat.Video,
        "file" or "files" => PowerScriptDataFormat.Files,
        _ => null,
    };

}
