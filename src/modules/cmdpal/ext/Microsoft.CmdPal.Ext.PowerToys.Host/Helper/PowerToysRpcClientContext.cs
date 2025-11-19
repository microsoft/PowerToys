// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace Microsoft.CmdPal.Ext.PowerToys.Helper;

[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Default)]
[JsonSerializable(typeof(PowerToysRpcClient.RpcRequest))]
internal sealed partial class PowerToysRpcClientContext : JsonSerializerContext
{
}
