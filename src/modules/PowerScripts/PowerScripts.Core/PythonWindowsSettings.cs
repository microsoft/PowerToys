// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using PowerScripts.Core.Storage;

namespace PowerScripts.Core;

/// <summary>Windows-mode Python settings.</summary>
public sealed class PythonWindowsSettings
{
    /// <summary>Gets or sets optional explicit interpreter path; empty means auto-detect (python.exe / python3.exe / py.exe).</summary>
    [JsonPropertyName("interpreterPath")]
    public string InterpreterPath { get; set; } = string.Empty;
}
