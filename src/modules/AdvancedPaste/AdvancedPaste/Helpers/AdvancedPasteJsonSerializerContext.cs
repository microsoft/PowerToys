// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;
using AdvancedPaste.Models.KernelQueryCache;

namespace AdvancedPaste.Helpers;

[JsonSerializable(typeof(PersistedCache))]
[JsonSerializable(typeof(LogEvent))]
[JsonSourceGenerationOptions(UseStringEnumConverter = true)]
public sealed partial class AdvancedPasteJsonSerializerContext : JsonSerializerContext
{
}
