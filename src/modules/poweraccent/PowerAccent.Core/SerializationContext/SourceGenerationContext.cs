// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;
using PowerAccent.Core.Services;

namespace PowerAccent.Core.SerializationContext;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(SettingsService))]
public partial class SourceGenerationContext : JsonSerializerContext
{
}
