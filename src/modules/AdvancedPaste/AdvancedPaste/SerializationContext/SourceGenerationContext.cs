// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;
using AdvancedPaste.Helpers;
using AdvancedPaste.Models.KernelQueryCache;

namespace AdvancedPaste.SerializationContext;

[JsonSerializable(typeof(PersistedCache))]
[JsonSerializable(typeof(AIServiceFormatEvent))]
[JsonSourceGenerationOptions(UseStringEnumConverter = true)]
public sealed partial class SourceGenerationContext : JsonSerializerContext
{
}
