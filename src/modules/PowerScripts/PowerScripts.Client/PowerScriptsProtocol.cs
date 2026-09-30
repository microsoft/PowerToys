// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerScripts.Client;

/// <summary>
/// The data shapes a PowerScript can consume or produce. A script declares only its own I/O; a
/// consuming module discovers the scripts it can use by filtering on these (see the filter methods on
/// <see cref="PowerScriptsClient"/>). These names match the host's <c>io</c> block exactly.
/// </summary>
public enum PowerScriptIO
{
    /// <summary>No data in/out (a system action with no input, or a side-effect with no output).</summary>
    None,

    /// <summary>Plain text.</summary>
    Text,

    /// <summary>HTML fragment.</summary>
    Html,

    /// <summary>An image.</summary>
    Image,

    /// <summary>Audio.</summary>
    Audio,

    /// <summary>Video.</summary>
    Video,

    /// <summary>One or more files (for a <c>file</c> script, see <see cref="PowerScriptInfo.InputExtensions"/>).</summary>
    Files,
}

/// <summary>
/// The stable strings and numbers of the PowerScripts host contract, in one place so consumers never
/// hard-code them. These mirror <c>PowerScripts.Host.exe</c> exactly.
/// </summary>
public static class PowerScriptsProtocol
{
    /// <summary>The host executable file name.</summary>
    public const string HostExeName = "PowerScripts.Host.exe";

    /// <summary>Environment variable that points directly at the host executable (used in dev builds).</summary>
    public const string HostPathEnvironmentVariable = "POWERSCRIPTS_HOST";

    /// <summary>Environment variable that overrides the scripts root the host scans.</summary>
    public const string RootEnvironmentVariable = "POWERSCRIPTS_ROOT";

    /// <summary>Parses a host <c>io</c> token (e.g. <c>text</c>, <c>files</c>, <c>none</c>) into <see cref="PowerScriptIO"/>.</summary>
    public static PowerScriptIO ParseIO(string? token) => token?.Trim().ToLowerInvariant() switch
    {
        "text" => PowerScriptIO.Text,
        "html" => PowerScriptIO.Html,
        "image" => PowerScriptIO.Image,
        "audio" => PowerScriptIO.Audio,
        "video" => PowerScriptIO.Video,
        "file" or "files" => PowerScriptIO.Files,
        _ => PowerScriptIO.None,
    };

    /// <summary>The exit codes the host returns. Consumers should branch on these rather than guessing.</summary>
    public static class ExitCode
    {
        /// <summary>Success.</summary>
        public const int Ok = 0;

        /// <summary>Bad usage or unknown script id.</summary>
        public const int Usage = 1;

        /// <summary>Unhandled error in the host.</summary>
        public const int Error = 2;

        /// <summary>Script is not trusted and consent was disabled (<c>--no-consent</c>): refused to run.</summary>
        public const int UntrustedNoConsent = 3;

        /// <summary>PowerScripts is disabled in PowerToys settings: refused to run.</summary>
        public const int Disabled = 4;

        /// <summary>The run timed out.</summary>
        public const int Timeout = 124;

        /// <summary>Refused to run elevated and could not drop to the normal-user token (fail closed).</summary>
        public const int RefusedElevated = 126;
    }
}
