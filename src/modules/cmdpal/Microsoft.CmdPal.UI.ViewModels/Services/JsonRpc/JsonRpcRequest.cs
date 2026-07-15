// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Microsoft.CmdPal.UI.ViewModels.Services.JsonRpc;

/// <summary>
/// Represents a JSON-RPC 2.0 request message. A request always carries an
/// <see cref="Id"/> and a <see cref="Method"/> and expects a matching response.
/// </summary>
public sealed class JsonRpcRequest
{
    /// <summary>
    /// Gets or sets the JSON-RPC protocol version. Always "2.0".
    /// </summary>
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; set; } = "2.0";

    /// <summary>
    /// Gets or sets the correlation identifier used to match the response to this request.
    /// </summary>
    [JsonPropertyName("id")]
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the name of the method to invoke on the remote peer.
    /// </summary>
    [JsonPropertyName("method")]
    public string Method { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the parameters passed to the method, or null when there are none.
    /// </summary>
    [JsonPropertyName("params")]
    public JsonNode? Params { get; set; }
}
