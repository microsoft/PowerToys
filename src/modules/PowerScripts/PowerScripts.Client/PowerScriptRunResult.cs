// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerScripts.Client;

/// <summary>The outcome of <c>run &lt;id&gt;</c>.</summary>
public sealed class PowerScriptRunResult
{
    /// <summary>Gets the host process exit code (see <see cref="PowerScriptsProtocol.ExitCode"/>).</summary>
    public int ExitCode { get; init; }

    /// <summary>Gets captured standard output.</summary>
    public string StdOut { get; init; } = string.Empty;

    /// <summary>Gets captured standard error.</summary>
    public string StdErr { get; init; } = string.Empty;

    /// <summary>Gets a value indicating whether true when the run completed successfully.</summary>
    public bool Succeeded => ExitCode == PowerScriptsProtocol.ExitCode.Ok;
}
