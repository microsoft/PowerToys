// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Serialization;
using PowerScripts.Core.Storage;

namespace PowerScripts.Core.Security;

/// <summary>
/// A single trust-on-first-use record: the user approved a script id whose content matched
/// <see cref="Hash"/>. If the script's content or declared capabilities later change, the recomputed
/// hash no longer matches and the user is asked to approve again.
/// </summary>
public sealed class TrustRecord
{
    public string Id { get; set; } = string.Empty;

    public string Hash { get; set; } = string.Empty;

    public IReadOnlyList<string> Capabilities { get; set; } = [];

    public string? Source { get; set; }

    public string? Publisher { get; set; }

    public DateTimeOffset ApprovedUtc { get; set; }
}
