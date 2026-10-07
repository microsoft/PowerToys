// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.CmdPal.Ext.Apps;

/// <summary>Provides Native AOT serialization metadata for the independently persisted Apps data files.</summary>
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(AppDataJsonContext.CommandAliasesFile), TypeInfoPropertyName = "CommandAliases")]
[JsonSerializable(typeof(AppDataJsonContext.VisibilityFile), TypeInfoPropertyName = "Visibility")]
internal partial class AppDataJsonContext : JsonSerializerContext
{
    internal sealed record CommandAliasesFile(IReadOnlyDictionary<string, string>? AppCommandAliases);

    internal sealed record VisibilityFile(string[]? HiddenAppIdentities);
}
