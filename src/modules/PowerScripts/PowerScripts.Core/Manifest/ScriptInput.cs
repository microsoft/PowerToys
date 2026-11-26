// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;
using PowerScripts.Core.Execution;

namespace PowerScripts.Core.Manifest;

/// <summary>
/// Declares the file input contract for a file-driven script (one whose input shape is
/// <see cref="PowerScriptDataFormat.Files"/>).
/// </summary>
public sealed class ScriptInput
{
    /// <summary>Gets or sets file extensions this script accepts (e.g. ".heic"). "*" means any extension.</summary>
    public List<string> Extensions { get; set; } = new();

    /// <summary>Gets or sets minimum number of files required.</summary>
    public int MinFiles { get; set; }

    /// <summary>Gets or sets maximum number of files; null means unbounded.</summary>
    public int? MaxFiles { get; set; }
}
