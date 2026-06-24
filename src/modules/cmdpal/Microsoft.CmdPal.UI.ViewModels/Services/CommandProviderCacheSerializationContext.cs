// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace Microsoft.CmdPal.UI.ViewModels.Services;

[JsonSerializable(typeof(CommandProviderCacheItem))]
[JsonSerializable(typeof(Dictionary<string, CommandProviderCacheItem>))]
[JsonSerializable(typeof(CommandProviderCacheContainer))]
[JsonSourceGenerationOptions(WriteIndented = true, PropertyNameCaseInsensitive = false)]
internal sealed partial class CommandProviderCacheSerializationContext : JsonSerializerContext;
