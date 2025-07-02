// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text.Json.Serialization;
using Microsoft.CmdPal.Ext.WebSearch.Helpers;

namespace Microsoft.CmdPal.Ext.WebSearch;

[JsonSerializable(typeof(float))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(HistoryItem))]
[JsonSerializable(typeof(List<HistoryItem>))]
[JsonSourceGenerationOptions(UseStringEnumConverter = true, WriteIndented = true, IncludeFields = true, PropertyNameCaseInsensitive = true, AllowTrailingCommas = true)]
internal sealed partial class WebSearchJsonSerializationContext : JsonSerializerContext
{
}
