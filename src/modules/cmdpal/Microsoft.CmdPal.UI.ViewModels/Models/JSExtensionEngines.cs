// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace Microsoft.CmdPal.UI.ViewModels.Models;

/// <summary>
/// Represents the "engines" section of a package.json.
/// </summary>
public sealed record JSExtensionEngines
{
    /// <summary>
    /// Gets the Node.js version requirement (for example, "&gt;=18").
    /// </summary>
    [JsonPropertyName("node")]
    public string? Node { get; init; }
}
