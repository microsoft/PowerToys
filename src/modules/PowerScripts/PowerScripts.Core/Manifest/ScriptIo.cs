// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using PowerScripts.Core.Execution;

namespace PowerScripts.Core.Manifest;

/// <summary>Primary data shapes derived from the authoritative input/output schemas.</summary>
public static class ScriptIo
{
    public static bool IsSupported(PowerScriptDataFormat format) =>
        format is PowerScriptDataFormat.None or PowerScriptDataFormat.Text or PowerScriptDataFormat.Files
            or PowerScriptDataFormat.Image or PowerScriptDataFormat.Audio;

    public static (PowerScriptDataFormat Input, PowerScriptDataFormat Output) Resolve(PowerScriptManifest manifest) =>
        (manifest.InputFormat, manifest.OutputFormat);

    public static string ToToken(PowerScriptDataFormat format) => format.ToString().ToLowerInvariant();

    public static PowerScriptDataFormat? TryParse(string? token) => token?.Trim().ToLowerInvariant() switch
    {
        "none" => PowerScriptDataFormat.None,
        "text" => PowerScriptDataFormat.Text,
        "files" => PowerScriptDataFormat.Files,
        "image" => PowerScriptDataFormat.Image,
        "audio" => PowerScriptDataFormat.Audio,
        _ => null,
    };
}
