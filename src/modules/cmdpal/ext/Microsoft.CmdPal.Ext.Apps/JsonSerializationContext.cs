// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text.Json.Serialization;
using Microsoft.CmdPal.Ext.Apps.State;

namespace Microsoft.CmdPal.Ext.Apps;

[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(PinnedApps))]
[JsonSerializable(typeof(List<string>), TypeInfoPropertyName = "StringList")]
[JsonSourceGenerationOptions(UseStringEnumConverter = true, WriteIndented = true, IncludeFields = true, PropertyNameCaseInsensitive = true, AllowTrailingCommas = true)]
internal sealed partial class JsonSerializationContext : JsonSerializerContext
{
}
