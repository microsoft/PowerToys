// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.CmdPal.UI.ViewModels.Services.JsonRpc;

/// <summary>
/// Represents a JSON-RPC 2.0 response message. A response carries the
/// <see cref="Id"/> of the request it answers and exactly one of
/// <see cref="Result"/> or <see cref="Error"/>.
/// </summary>
public sealed class JsonRpcResponse
{
    /// <summary>
    /// Gets or sets the JSON-RPC protocol version. Always "2.0".
    /// </summary>
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; set; } = "2.0";

    /// <summary>
    /// Gets or sets the correlation identifier copied from the originating request.
    /// </summary>
    [JsonPropertyName("id")]
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the result payload when the call succeeded.
    /// </summary>
    [JsonPropertyName("result")]
    public JsonElement? Result { get; set; }

    /// <summary>
    /// Gets or sets the error object when the call failed.
    /// </summary>
    [JsonPropertyName("error")]
    public JsonRpcError? Error { get; set; }
}
