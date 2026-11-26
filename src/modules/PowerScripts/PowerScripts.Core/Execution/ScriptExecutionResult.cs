// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using PowerScripts.Core.Cli;
using PowerScripts.Core.Manifest;
using PowerScripts.Core.Security;

namespace PowerScripts.Core.Execution;

/// <summary>
/// The outcome of running a PowerScript.
/// </summary>
public sealed class ScriptExecutionResult
{
    public int ExitCode { get; init; }

    public bool Succeeded => ExitCode == 0;

    public string StdOut { get; init; } = string.Empty;

    public string StdErr { get; init; } = string.Empty;
}
