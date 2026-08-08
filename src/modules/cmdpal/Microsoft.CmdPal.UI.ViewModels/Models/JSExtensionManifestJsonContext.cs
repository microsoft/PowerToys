// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace Microsoft.CmdPal.UI.ViewModels.Models;

/// <summary>
/// Source-generated JSON serialization context for the extension manifest types.
/// </summary>
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true, AllowTrailingCommas = true, ReadCommentHandling = System.Text.Json.JsonCommentHandling.Skip)]
[JsonSerializable(typeof(JSPackageJson))]
[JsonSerializable(typeof(JSCmdPalSection))]
[JsonSerializable(typeof(JSExtensionEngines))]
[JsonSerializable(typeof(string[]))]
internal sealed partial class JSExtensionManifestJsonContext : JsonSerializerContext
{
}
