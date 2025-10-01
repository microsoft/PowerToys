// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System.Text.Json.Serialization;

namespace Microsoft.CmdPal.Ext.WindowsTerminal.Helpers;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(AppSettings))]
internal sealed partial class AppSettingsJsonContext : JsonSerializerContext;
