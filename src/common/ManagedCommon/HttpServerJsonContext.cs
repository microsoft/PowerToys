// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ManagedCommon
{
    [JsonSerializable(typeof(ServerStatusResponse))]
    [JsonSerializable(typeof(ModuleStatusResponse))]
    [JsonSerializable(typeof(GlobalStatusResponse))]
    [JsonSerializable(typeof(ErrorResponse))]
    [JsonSerializable(typeof(Dictionary<string, ModuleStatusResponse>))]
    [JsonSerializable(typeof(string[]))]
    [JsonSerializable(typeof(IEnumerable<string>))]
    [JsonSerializable(typeof(object))]
    internal sealed partial class HttpServerJsonContext : JsonSerializerContext
    {
    }
}
