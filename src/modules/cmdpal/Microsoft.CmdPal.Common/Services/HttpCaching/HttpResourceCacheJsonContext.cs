// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;
using Microsoft.CmdPal.Common.Services.HttpCaching.Abstraction;

namespace Microsoft.CmdPal.Common.Services.HttpCaching;

[JsonSerializable(typeof(HttpResourceCacheMetadata))]
internal sealed partial class HttpResourceCacheJsonContext : JsonSerializerContext
{
}
