// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace Microsoft.CommandPalette.Extensions.Toolkit;

[JsonSerializable(typeof(float))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(ChoiceSetCardSetting.Entry))]
[JsonSerializable(typeof(ChoiceSetSetting.Choice))]
[JsonSerializable(typeof(List<ChoiceSetCardSetting.Entry>))]
[JsonSerializable(typeof(List<ChoiceSetSetting.Choice>))]
[JsonSerializable(typeof(List<ChoiceSetCardSetting>))]
[JsonSerializable(typeof(List<ChoiceSetSetting>))]
[JsonSerializable(typeof(Dictionary<string, object>), TypeInfoPropertyName = "Dictionary")]
[JsonSerializable(typeof(List<Dictionary<string, object>>))]
[JsonSourceGenerationOptions(UseStringEnumConverter = true, WriteIndented = true)]
internal sealed partial class JsonSerializationContext : JsonSerializerContext
{
}
