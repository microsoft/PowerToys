// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Microsoft.CmdPal.UI.ViewModels.Services.JsonRpc;

/// <summary>
/// Represents a JSON-RPC 2.0 notification message. A notification carries a
/// <see cref="Method"/> but no id and never receives a response.
/// </summary>
public sealed class JsonRpcNotification
{
    /// <summary>
    /// Gets or sets the JSON-RPC protocol version. Always "2.0".
    /// </summary>
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; set; } = "2.0";

    /// <summary>
    /// Gets or sets the name of the method being notified.
    /// </summary>
    [JsonPropertyName("method")]
    public string Method { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the parameters for the notification, or null when there are none.
    /// </summary>
    [JsonPropertyName("params")]
    public JsonNode? Params { get; set; }
}
