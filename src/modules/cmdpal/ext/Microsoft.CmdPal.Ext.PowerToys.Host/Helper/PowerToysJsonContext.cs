// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace Microsoft.CmdPal.Ext.PowerToys.Helper;

// Source-generated JSON serialization context
[JsonSerializable(typeof(WorkspaceItemsHelper.WorkspacesData))]
[JsonSerializable(typeof(WorkspaceItemsHelper.WorkspaceProject))]
[JsonSerializable(typeof(WorkspaceItemsHelper.WorkspaceApplication))]
[JsonSerializable(typeof(List<string>))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true)]
internal sealed partial class PowerToysJsonContext : JsonSerializerContext
{
}
