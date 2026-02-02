// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.CmdPal.Ext.Calc.Helper;

[JsonSerializable(typeof(DateTime))]
[JsonSerializable(typeof(Guid))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(HistoryItem))]
[JsonSerializable(typeof(List<HistoryItem>), TypeInfoPropertyName = "HistoryItemList")]
[JsonSourceGenerationOptions(UseStringEnumConverter = true, WriteIndented = true, IncludeFields = true, PropertyNameCaseInsensitive = true, AllowTrailingCommas = true)]
internal sealed partial class CalcJsonSerializationContext : JsonSerializerContext;
