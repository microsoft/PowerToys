// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;
using PowerScripts.Core.Execution;

namespace PowerScripts.Core.Manifest;

/// <summary>
/// The execution source used by a PowerScript, including language runtimes and installed operations.
/// </summary>
public enum ScriptRuntime
{
    PowerShell,

    /// <summary>
    /// A Python script executed from its top-level statements. Runs on native Windows Python or
    /// inside WSL depending on the user's PowerScripts Python settings.
    /// </summary>
    Python,

    /// <summary>A known installed PowerToys operation described by a trusted JSON data entry.</summary>
    PowerToysOperation,
}
