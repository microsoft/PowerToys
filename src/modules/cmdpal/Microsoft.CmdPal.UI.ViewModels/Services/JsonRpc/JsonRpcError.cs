// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Microsoft.CmdPal.UI.ViewModels.Services.JsonRpc;

/// <summary>
/// Represents a JSON-RPC 2.0 error object returned inside a failed response.
/// </summary>
public sealed class JsonRpcError
{
    /// <summary>
    /// The JSON was malformed and could not be parsed.
    /// </summary>
    public const int ParseError = -32700;

    /// <summary>
    /// The payload was not a valid JSON-RPC request object.
    /// </summary>
    public const int InvalidRequest = -32600;

    /// <summary>
    /// The requested method does not exist or is not available.
    /// </summary>
    public const int MethodNotFound = -32601;

    /// <summary>
    /// The parameters supplied to the method were invalid.
    /// </summary>
    public const int InvalidParams = -32602;

    /// <summary>
    /// An unexpected error occurred while handling the request.
    /// </summary>
    public const int InternalError = -32603;

    /// <summary>
    /// Gets or sets the numeric error code.
    /// </summary>
    [JsonPropertyName("code")]
    public int Code { get; set; }

    /// <summary>
    /// Gets or sets the human-readable error message.
    /// </summary>
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets optional structured data describing the error.
    /// </summary>
    [JsonPropertyName("data")]
    public JsonNode? Data { get; set; }
}
