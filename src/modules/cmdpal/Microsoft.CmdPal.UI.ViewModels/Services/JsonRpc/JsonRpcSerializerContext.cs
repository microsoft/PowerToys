// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Microsoft.CmdPal.UI.ViewModels.Services.JsonRpc;

/// <summary>
/// Source-generated JSON serialization context for the JSON-RPC message types.
/// Using source generation keeps the transport trimming and AOT compatible.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(JsonRpcRequest))]
[JsonSerializable(typeof(JsonRpcResponse))]
[JsonSerializable(typeof(JsonRpcNotification))]
[JsonSerializable(typeof(JsonRpcError))]
[JsonSerializable(typeof(JsonElement))]
[JsonSerializable(typeof(JsonNode))]
[JsonSerializable(typeof(JsonObject))]
[JsonSerializable(typeof(JsonArray))]
internal sealed partial class JsonRpcSerializerContext : JsonSerializerContext
{
}
