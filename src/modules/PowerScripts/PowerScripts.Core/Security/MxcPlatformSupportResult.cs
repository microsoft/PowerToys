// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Text.Json;

namespace PowerScripts.Core.Security;

/// <summary>The result of probing whether this host can run PowerScripts through MXC.</summary>
public sealed class MxcPlatformSupportResult
{
    public bool IsSupported { get; init; }

    public string Reason { get; init; } = string.Empty;

    public int WindowsBuild { get; init; }

    public string ExecutorPath { get; init; } = string.Empty;

    public string Tier { get; init; } = string.Empty;

    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
}
