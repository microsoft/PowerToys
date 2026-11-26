// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace PowerScripts.Core.Manifest;

/// <summary>
/// The optional <c>x-execute</c> extension describes a custom executable and input transport.
/// </summary>
public sealed class ScriptExecute
{
    /// <summary>Gets or sets a closed built-in execution provider, mutually exclusive with a process recipe.</summary>
    [JsonPropertyName("provider")]
    public string? Provider { get; set; }

    /// <summary>
    /// Gets or sets the command and its fixed leading arguments, e.g. <c>["python3", "narrate_image.py"]</c>. The
    /// first token is the interpreter; a token that names the sibling script is resolved to its full
    /// path at run time.
    /// </summary>
    [JsonPropertyName("command")]
    public List<string>? Command { get; set; }

    /// <summary>
    /// Gets or sets maps an input property name to the command-line flag that carries it (e.g.
    /// <c>{"voice": "--voice"}</c>). Names absent from the map are passed as <c>--&lt;name&gt;</c>.
    /// </summary>
    [JsonPropertyName("argMap")]
    public Dictionary<string, string>? ArgMap { get; set; }

    /// <summary>
    /// Gets or sets when <c>"json"</c>, the whole input object is written to the process' stdin as a single JSON
    /// document instead of being spread across argv — the channel for large / structured inputs.
    /// </summary>
    [JsonPropertyName("stdin")]
    public string? Stdin { get; set; }

    /// <summary>Gets or sets optional human meaning of non-zero exit codes, surfaced in errors.</summary>
    [JsonPropertyName("exitCodes")]
    public Dictionary<string, string>? ExitCodes { get; set; }

    /// <summary>Gets or sets retains unknown recipe fields so providers can reject them without changing legacy recipes.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalProperties { get; set; }

    /// <summary>Gets a value indicating whether true when the structured input should be delivered on stdin as JSON.</summary>
    [JsonIgnore]
    public bool UsesJsonStdin => string.Equals(Stdin, "json", StringComparison.OrdinalIgnoreCase);
}
