// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;
using static Microsoft.CommandPalette.Extensions.Toolkit.ChoiceSetSetting;

namespace Microsoft.CommandPalette.Extensions.Toolkit;

[JsonSerializable(typeof(float))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(Choice))]
[JsonSerializable(typeof(List<Choice>))]
[JsonSerializable(typeof(List<ChoiceSetSetting>))]
[JsonSerializable(typeof(Dictionary<string, object>), TypeInfoPropertyName = "Dictionary")]
[JsonSerializable(typeof(List<Dictionary<string, object>>))]
[JsonSourceGenerationOptions(UseStringEnumConverter = true, WriteIndented = true)]
internal sealed partial class JsonSerializationContext : JsonSerializerContext
{
}
