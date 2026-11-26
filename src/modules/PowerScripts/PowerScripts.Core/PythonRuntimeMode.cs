// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using PowerScripts.Core.Storage;

namespace PowerScripts.Core;

/// <summary>How Python PowerScripts are executed.</summary>
public enum PythonRuntimeMode
{
    /// <summary>Python PowerScripts are not run.</summary>
    Disabled,

    /// <summary>Run using native Windows Python (auto-detected or a configured interpreter path).</summary>
    Windows,

    /// <summary>Run in WSLC when global MXC is enabled, or in the configured WSL distribution otherwise.</summary>
    Wsl,
}
